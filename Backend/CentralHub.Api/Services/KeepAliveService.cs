using CentralHub.Api.Data;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Serviço em segundo plano que, a cada 30 segundos, percorre todas as
/// Centrais cadastradas, verifica a conectividade real e atualiza os
/// campos Status e Latencia no banco de dados.
/// </summary>
public class KeepAliveService : BackgroundService
{
    public const string StatusDesconhecido = "Desconhecido";

    /// <summary>Limite de centrais verificadas ao mesmo tempo, para não abrir conexões demais de uma vez.</summary>
    public const int MaxVerificacoesSimultaneas = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KeepAliveService> _logger;
    private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(30);

    public KeepAliveService(IServiceProvider serviceProvider, ILogger<KeepAliveService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KeepAliveService iniciado. Intervalo de verificação: {Intervalo}s", _intervalo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await VerificarCentraisAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Uma falha ao verificar uma central não deve derrubar o serviço em segundo plano.
                _logger.LogError(ex, "Erro ao executar verificação periódica do KeepAliveService");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }

    /// <summary>Executa uma rodada de verificação de todas as centrais cadastradas.</summary>
    public async Task VerificarCentraisAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adapterResolver = scope.ServiceProvider.GetRequiredService<IAdapterResolver>();
        var centrals = await context.Centrals.ToListAsync(cancellationToken);

        _logger.LogInformation("KeepAlive: verificando {Quantidade} central(is)", centrals.Count);

        // As verificações rodam em paralelo (no máximo MaxVerificacoesSimultaneas por vez) e não tocam
        // no DbContext: só devolvem o resultado. As gravações acontecem depois, em uma única thread.
        using var vagas = new SemaphoreSlim(MaxVerificacoesSimultaneas);
        var verificacoes = centrals.Select(central => VerificarAsync(central, adapterResolver, vagas, cancellationToken));
        var resultados = await Task.WhenAll(verificacoes);

        foreach (var (central, resultado) in centrals.Zip(resultados))
        {
            if (resultado == null)
            {
                continue; // a verificação falhou com exceção: mantém o último status conhecido
            }

            var statusAnterior = central.Status;
            central.Status = resultado.Status;
            central.Latencia = resultado.Latencia;

            if (statusAnterior != central.Status)
            {
                _logger.LogInformation(
                    "Central {CentralId}: status alterado de {StatusAnterior} para {StatusAtual} (latência {Latencia}ms)",
                    central.Id, statusAnterior, central.Status, central.Latencia);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record ResultadoVerificacao(string Status, long? Latencia);

    private async Task<ResultadoVerificacao?> VerificarAsync(
        Central central, IAdapterResolver adapterResolver, SemaphoreSlim vagas, CancellationToken cancellationToken)
    {
        await vagas.WaitAsync(cancellationToken);
        try
        {
            var adapter = adapterResolver.Resolver(central.Fabricante);
            if (adapter == null)
            {
                // Fabricante não reconhecido: não há como verificar, e o FakeAdapter não pode mascarar isso.
                return new ResultadoVerificacao(StatusDesconhecido, null);
            }

            var resultado = await adapter.VerificarConectividade(central.IP, central.Porta, central.Usuario, central.Senha);
            return new ResultadoVerificacao(resultado.Status, resultado.LatenciaMs);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Uma central com problema não pode impedir a atualização das demais.
            _logger.LogError(ex, "KeepAlive: erro ao verificar a Central {CentralId}", central.Id);
            return null;
        }
        finally
        {
            vagas.Release();
        }
    }
}
