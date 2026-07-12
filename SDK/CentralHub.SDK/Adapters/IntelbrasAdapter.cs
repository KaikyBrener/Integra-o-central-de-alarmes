namespace CentralHub.SDK.Adapters;

public class IntelbrasAdapter : TcpAdapterBase
{
    /// <summary>Porta padrao do Receptor IP Intelbras (AMT), usada como heuristica de deteccao.</summary>
    public const int PortaPadrao = 9009;

    /// <inheritdoc/>
    protected override Task<ConexaoResult> IdentificarAsync(
        string ip, int porta, string usuario, string senha, long latenciaMs)
    {
        // TODO: identificar Modelo e Firmware reais via protocolo ISECNet (opcode 0x0b4a).
        // Requer solicitacao do manual ao suporte Intelbras (WhatsApp, sem NDA).
        return Task.FromResult(new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "Intelbras",
            Modelo = "Desconhecido",
            Firmware = "Desconhecido",
            Status = "Online",
            LatenciaMs = latenciaMs
        });
    }

    // AcionarPGM / DesligarPGM / PulsoPGM: herdados de TcpAdapterBase.
    // Retornam "Protocolo nao implementado." ate o ISECNet ser integrado.
}
