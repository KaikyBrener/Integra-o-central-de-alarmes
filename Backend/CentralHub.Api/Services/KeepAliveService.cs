using CentralHub.Api.Data;
using CentralHub.SDK.Adapters;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Serviço em segundo plano que, a cada 30 segundos, percorre todas as
/// Centrais cadastradas, verifica a conectividade real e atualiza os
/// campos Status e Latencia no banco de dados.
/// </summary>
public class KeepAliveService : BackgroundService
{
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
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var centrals = await context.Centrals.ToListAsync(stoppingToken);

                _logger.LogInformation("KeepAlive: verificando {Quantidade} central(is)", centrals.Count);

                foreach (var central in centrals)
                {
                    var adapter = AdapterFactory.Criar(AdapterFactory.ResolverPorNome(central.Fabricante));
                    var resultado = await adapter.VerificarConectividade(central.IP, central.Porta, central.Usuario, central.Senha);

                    var statusAnterior = central.Status;
                    central.Status = resultado.Status;
                    central.Latencia = resultado.LatenciaMs;

                    if (statusAnterior != central.Status)
                    {
                        _logger.LogInformation(
                            "Central {CentralId}: status alterado de {StatusAnterior} para {StatusAtual} (latência {Latencia}ms)",
                            central.Id, statusAnterior, central.Status, central.Latencia);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Uma falha ao verificar uma central não deve derrubar o serviço em segundo plano.
                _logger.LogError(ex, "Erro ao executar verificação periódica do KeepAliveService");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }
}
