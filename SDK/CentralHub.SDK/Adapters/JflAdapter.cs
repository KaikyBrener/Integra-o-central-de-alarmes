namespace CentralHub.SDK.Adapters;

public class JflAdapter : TcpAdapterBase
{
    /// <summary>Porta padrao da central JFL Active (Ethernet), usada como heuristica de deteccao.</summary>
    public const int PortaPadrao = 8085;

    /// <inheritdoc/>
    protected override Task<ConexaoResult> IdentificarAsync(
        string ip, int porta, string usuario, string senha, long latenciaMs)
    {
        // TODO: identificar Modelo e Firmware reais via protocolo JFL Active.
        // A central JFL conecta-se como cliente TCP; o sistema deve atuar como servidor.
        // Requer PDF oficial do protocolo JFL ou captura Wireshark para implementacao.
        return Task.FromResult(new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "JFL",
            Modelo = "Desconhecido",
            Firmware = "Desconhecido",
            Status = "Online",
            LatenciaMs = latenciaMs
        });
    }

    // AcionarPGM / DesligarPGM / PulsoPGM: herdados de TcpAdapterBase.
    // Retornam "Protocolo nao implementado." ate o protocolo JFL ser integrado.
}
