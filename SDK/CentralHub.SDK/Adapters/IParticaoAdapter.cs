namespace CentralHub.SDK.Adapters;

public enum ParticaoEstado
{
    NaoProgramada,
    Desarmada,
    ArmadaTotal,
    ArmadaStay
}

/// <summary>Estado de uma particao retornado por consulta a central.</summary>
public class ParticaoResult
{
    public int Numero { get; set; }
    public ParticaoEstado Estado { get; set; }
    public bool EmAlarme { get; set; }
}

/// <summary>
/// Suporte opcional a consulta, arme e desarme de particoes. Fora de <see cref="ICentralAdapter"/> pelo
/// mesmo motivo de <see cref="IZonaAdapter"/>: so o adapter cujo protocolo sustenta a operacao a implementa.
/// </summary>
public interface IParticaoAdapter
{
    Task<IEnumerable<ParticaoResult>> ConsultarParticoes(string ip, int porta, string usuario, string senha);

    Task<ComandoResult> ArmarParticao(string ip, int porta, string usuario, string senha, int particao);

    Task<ComandoResult> DesarmarParticao(string ip, int porta, string usuario, string senha, int particao);
}
