namespace CentralHub.SDK.Adapters;

public class ConexaoResult
{
    public bool Sucesso { get; set; }
    public string Fabricante { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Firmware { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long LatenciaMs { get; set; }
    public string? Erro { get; set; }
}

public class ComandoResult
{
    public bool Sucesso { get; set; }
    public string Resultado { get; set; } = string.Empty;
}

public interface ICentralAdapter
{
    Task<bool> Conectar(string ip, int porta, string usuario, string senha);
    Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha);
    Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm);
    Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm);
    Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs);
}
