namespace CentralHub.SDK.Adapters;

/// <summary>
/// Estado possível de uma zona da central de alarme.
/// TODO: o mapeamento definitivo depende do protocolo de cada fabricante.
/// Intelbras (ISECNet) já tem o opcode de bypass mapeado
/// (<see cref="IsecNetProtocol.OpcodeBypassZona"/>), mas não há confirmação
/// pública do opcode de consulta de status (aberta/fechada/disparo).
/// JFL: nenhum opcode de zona encontrado nas fontes públicas.
/// </summary>
public enum ZonaStatus
{
    Fechada,
    Aberta,
    Disparo,
    Bypass,
    /// <summary>Tamper, curto-circuito, bateria fraca ou falta de comunicacao do sensor (ver Descricao).</summary>
    Problema
}

/// <summary>Estado de uma zona, retornado por consulta à central.</summary>
public class ZonaResult
{
    public int Numero { get; set; }
    public ZonaStatus Status { get; set; }
    public string? Descricao { get; set; }
}
