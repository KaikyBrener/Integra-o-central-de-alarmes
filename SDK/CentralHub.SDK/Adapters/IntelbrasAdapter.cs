namespace CentralHub.SDK.Adapters;

public class IntelbrasAdapter : ICentralAdapter
{
    /// <summary>Porta padrão de fábrica do receptor IP Intelbras (AMT), usada como heurística de detecção.</summary>
    public const int PortaPadrao = 9009;


    public async Task<bool> Conectar(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, _, _) = await TcpConnectionHelper.TestarConexao(ip, porta);
        return sucesso;
    }

    public async Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, latenciaMs, erro) = await TcpConnectionHelper.TestarConexao(ip, porta);

        if (!sucesso)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = erro
            };
        }

        // TODO: identificar Modelo/Firmware reais via protocolo proprietario Intelbras.
        return new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "Intelbras",
            Modelo = "AMT 8000",
            Firmware = "2.4.1",
            Status = "Online",
            LatenciaMs = latenciaMs
        };
    }

    public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} ligado" });
    }

    public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} desligado" });
    }

    public Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
    {
        // TODO: implementar comando real do protocolo Intelbras.
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} pulso de {tempoMs}ms" });
    }
}
