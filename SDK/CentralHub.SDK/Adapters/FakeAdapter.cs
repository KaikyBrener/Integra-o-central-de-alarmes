namespace CentralHub.SDK.Adapters;

/// <summary>
/// Adapter simulado, usado para desenvolvimento e testes sem hardware real.
/// </summary>
public class FakeAdapter : ICentralAdapter
{
    public Task<bool> Conectar(string ip, int porta, string usuario, string senha)
    {
        return Task.FromResult(true);
    }

    public Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
    {
        var result = new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "Fake",
            Modelo = "FK-100",
            Firmware = "1.0.0",
            Status = "Online",
            LatenciaMs = 10
        };
        return Task.FromResult(result);
    }

    public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} ligado" });
    }

    public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
    {
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} desligado" });
    }

    public Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
    {
        return Task.FromResult(new ComandoResult { Sucesso = true, Resultado = $"PGM {pgm} pulso de {tempoMs}ms" });
    }
}
