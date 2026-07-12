using CentralHub.SDK.Adapters;

namespace CentralHub.Api.Services;

/// <summary>
/// Responsável por testar a conectividade com uma Central, selecionando
/// automaticamente o adapter mais adequado via AdapterFactory.
/// </summary>
public class ConnectionService
{
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
    }

    public async Task<ConexaoResult> TestarConexao(string ip, int porta, string usuario, string senha)
    {
        _logger.LogInformation("Testando conexão com Central em {IP}:{Porta}", ip, porta);

        var resultado = await AdapterFactory.DetectarEConectar(ip, porta, usuario, senha);

        if (resultado.Sucesso)
        {
            _logger.LogInformation(
                "Conexão bem-sucedida com {IP}:{Porta} - Fabricante: {Fabricante}, Latência: {Latencia}ms",
                ip, porta, resultado.Fabricante, resultado.LatenciaMs);
        }
        else
        {
            _logger.LogWarning("Falha ao conectar com {IP}:{Porta} - {Erro}", ip, porta, resultado.Erro);
        }

        return resultado;
    }
}
