using CentralHub.Api.Data;
using CentralHub.Api.Models;
using CentralHub.SDK.Adapters;
using CentralHub.SDK.Adapters.Jfl;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Opções do servidor de monitoramento JFL (seção "Jfl" do appsettings.json).
/// </summary>
public class JflOptions
{
    public const string Secao = "Jfl";

    /// <summary>Desliga o servidor de escuta (útil em ambientes sem centrais JFL).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Porta que as centrais JFL usam como destino (endereços 706/707 da central; no máximo 9999).</summary>
    public int Port { get; set; } = 9090;

    public string BindAddress { get; set; } = "0.0.0.0";

    public int KeepAliveMinutes { get; set; } = 5;

    /// <summary>Quando false, só centrais cadastradas (Fabricante JFL e Usuário = nº de série) são aceitas.</summary>
    public bool AcceptUnknownPanels { get; set; } = true;

    /// <summary>Recusa qualquer comando que altere a central (PGM, arme, bypass).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Registra em log todos os quadros (hex) trocados com as centrais. Use para validar modelos/firmwares.</summary>
    public bool LogRawFrames { get; set; }
}

/// <summary>
/// Hospeda o servidor TCP que faz o papel do Active NET: aceita as conexões das centrais JFL, confirma
/// eventos/keep-alive, grava os eventos recebidos e mantém o status das centrais cadastradas.
/// </summary>
public class JflListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JflOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<JflListenerService> _logger;

    public JflListenerService(
        IServiceScopeFactory scopeFactory,
        Microsoft.Extensions.Options.IOptions<JflOptions> options,
        ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<JflListenerService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        JflAdapter.DefaultOptions = new JflAdapterOptions { ReadOnly = _options.ReadOnly };

        if (!_options.Enabled)
        {
            _logger.LogInformation("Servidor de monitoramento JFL desabilitado (Jfl:Enabled = false)");
            return;
        }

        var server = new JflServer(
            new JflServerOptions
            {
                Port = _options.Port,
                BindAddress = _options.BindAddress,
                KeepAliveMinutes = _options.KeepAliveMinutes,
                AcceptUnknownPanels = _options.AcceptUnknownPanels,
                LogRawFrames = _options.LogRawFrames,
                AcceptPanel = serial => CentralCadastrada(serial)
            },
            JflSessionRegistry.Default,
            _loggerFactory.CreateLogger("CentralHub.Jfl"));

        // Fila simples para não bloquear o loop de rede com acesso a banco.
        var fila = System.Threading.Channels.Channel.CreateUnbounded<EventoCentral>();
        server.EventoRecebido += e => fila.Writer.TryWrite(e);
        server.PanelConnected += s => _ = AtualizarCentralAsync(s, "Online");
        server.PanelDisconnected += s => _ = AtualizarCentralAsync(s, "Offline");

        try
        {
            server.Start(stoppingToken);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or FormatException)
        {
            _logger.LogError(ex, "Não foi possível iniciar o servidor JFL em {Address}:{Port}. Eventos e comandos JFL ficam indisponíveis.",
                _options.BindAddress, _options.Port);
            return;
        }

        try
        {
            await foreach (var evento in fila.Reader.ReadAllAsync(stoppingToken))
            {
                await GravarEventoAsync(evento);
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal
        }
        finally
        {
            await server.StopAsync();
        }
    }

    private bool CentralCadastrada(string serial)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return context.Centrals.AsNoTracking().Any(c => c.Usuario == serial && c.Fabricante != null && c.Fabricante.ToLower() == "jfl");
    }

    private async Task GravarEventoAsync(EventoCentral e)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var centralId = e.Serial is null
                ? (int?)null
                : await context.Centrals.AsNoTracking()
                    .Where(c => c.Usuario == e.Serial && c.Fabricante != null && c.Fabricante.ToLower() == "jfl")
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync();

            context.Eventos.Add(new CentralEvent
            {
                DataHora = e.DataHora,
                CentralId = centralId,
                Fabricante = e.Fabricante,
                Serial = e.Serial,
                Conta = e.Conta,
                Codigo = e.Codigo,
                Tipo = e.Tipo.ToString(),
                Restauracao = e.Restauracao,
                Particao = e.Particao,
                Zona = e.Zona,
                Usuario = e.Usuario,
                Pgm = e.Pgm,
                Descricao = e.Detalhe,
                Bruto = e.Bruto
            });
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // O evento já foi confirmado à central; falha de banco não pode derrubar o servidor.
            _logger.LogError(ex, "Falha ao gravar evento {Codigo} da central {Serial}", e.Codigo, e.Serial);
        }
    }

    private async Task AtualizarCentralAsync(JflSession session, string status)
    {
        try
        {
            if (session.Serial is null) return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var centrais = await context.Centrals
                .Where(c => c.Usuario == session.Serial && c.Fabricante != null && c.Fabricante.ToLower() == "jfl")
                .ToListAsync();

            foreach (var central in centrais)
            {
                central.Status = status;
                if (status == "Online" && session.Profile is not null && session.Info is not null)
                {
                    central.Modelo = session.Profile.Description;
                    central.Firmware = session.Info.Firmware;
                }
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar o status da central JFL {Serial}", session.Serial);
        }
    }
}
