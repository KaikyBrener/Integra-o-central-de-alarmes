namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>Bytes de comando do quadro JFL 0x7B. Somente os que tem evidencia e sao usados.</summary>
public static class JflCmd
{
    /// <summary>Central se apresenta (102 bytes). Carrega o numero de serie.</summary>
    public const byte Connection = 0x21;
    /// <summary>Evento Contact ID (24 bytes). Deve ser confirmado, senao a central repete indefinidamente.</summary>
    public const byte Event = 0x24;
    /// <summary>Keep-alive enviado pela central (5 bytes).</summary>
    public const byte KeepAlive = 0x40;
    /// <summary>Pedido de status. A central so envia status sob demanda.</summary>
    public const byte Status = 0x4D;
    public const byte Arm = 0x4E;
    public const byte Disarm = 0x4F;
    public const byte PgmOn = 0x50;
    public const byte PgmOff = 0x51;
    public const byte Bypass = 0x52;
    public const byte StatusUser = 0x56;

    /// <summary>
    /// Comandos aos quais a central responde com um quadro de status completo (eco do CMD).
    /// A familia autenticada 0x37 propositalmente NAO e usada: 5 senhas erradas bloqueiam o
    /// acesso remoto da central.
    /// </summary>
    public static bool RepliesWithStatus(byte cmd) =>
        cmd is Status or StatusUser or Arm or Disarm or PgmOn or PgmOff or Bypass;
}

public enum JflZoneStatus
{
    Disabled = 0x0,
    /// <summary>Anulada manualmente. Zona auto-anulada continua informando o estado fisico.</summary>
    Bypassed = 0x1,
    Triggered = 0x2,
    NotCommunicating = 0x3,
    ShortCircuit = 0x4,
    Tamper = 0x5,
    LowBattery = 0x6,
    Open = 0x7,
    Closed = 0x8
}

public sealed record JflZone(int Number, JflZoneStatus Status, bool Bypassable);

/// <summary>Estado de uma particao: 0x00 nao programada, 0x01 desarmada, 0x02 armada AWAY, 0x03 STAY; bit 7 = em alarme.</summary>
public readonly record struct JflPartitionState(byte Raw)
{
    public bool Programmed => Raw != 0x00;
    public int Mode => Raw & 0x7F;
    public bool InAlarm => (Raw & 0x80) != 0;
    public bool Disarmed => Mode == 0x01;
    public bool ArmedAway => Mode == 0x02;
    public bool ArmedStay => Mode == 0x03;
    public bool Armed => ArmedAway || ArmedStay;
}

/// <summary>Quadro 0x21: identificacao da central. Layout absoluto em <see cref="JflDecoder.DecodeConnection"/>.</summary>
public sealed record JflConnectionInfo(
    string Serial,
    byte ModelByte,
    string Firmware,
    string Mac,
    string Imei,
    int PartitionCount,
    int Signal,
    byte FenceRaw,
    IReadOnlyList<JflPartitionState> Partitions);

/// <summary>Evento Contact ID (quadro 0x24).</summary>
public sealed record JflPanelEvent(
    string Account,
    string Code,
    string Partition,
    string Subject,
    byte[] Counter,
    byte FenceRaw,
    byte Problem,
    byte Seq)
{
    /// <summary>Primeiro digito do codigo (Q): 1 = evento/novo, 3 = restauracao.</summary>
    public char Qualifier => Code.Length > 0 ? Code[0] : '?';
}

public enum JflProblem
{
    WirelessLowBattery = 0, SensorSupervision = 1, AuxiliaryOutput = 2, Tamper = 3, Dhcp = 4,
    NetworkCable = 5, CellularModule = 6, Sms = 7,
    Ethernet = 8, Gprs = 9, TelephoneLine = 10, Short = 11, Keypad = 12, Siren = 13, Battery = 14, AcMains = 15,
    BatteryReversed = 16, DestinationIp2 = 17, DestinationIp1 = 18, DnsServer = 19, KeypadAc = 20,
    SirenSupervision = 21, WirelessPassword = 22, WirelessAuthentication = 23,
    SsidNotFound = 24, IpConflict = 25, Bus = 26, Ddns = 27, Notification = 28, EthernetModule = 29,
    SignalLevel = 30, SimCard = 31
}

/// <summary>Quadro de status (resposta a 0x4D e a todo comando sem autenticacao).</summary>
public sealed record JflStatus(
    string Clock,
    double BatteryVolts,
    int PgmState,
    IReadOnlyList<JflPartitionState> Partitions,
    byte FenceRaw,
    IReadOnlyList<JflZone> Zones,
    IReadOnlyList<JflProblem> Problems,
    int PgmPermissions,
    IReadOnlyList<int> BypassableZones,
    int FrameLength,
    byte Seq)
{
    /// <summary>PGM 1..16. LSB do byte 13 = PGM 1; byte 117 = PGM 9..16.</summary>
    public bool IsPgmOn(int pgm) => Bit(PgmState, pgm);

    /// <summary>Bit zerado = a central recusa o comando (PGM nao programada ou sem funcao "acionada pelo usuario").</summary>
    public bool IsPgmPermitted(int pgm) => Bit(PgmPermissions, pgm);

    /// <summary>True quando o quadro e curto demais para conter o byte de permissao da PGM (nao ha como afirmar nada).</summary>
    public bool PgmPermissionUnknown(int pgm) => pgm <= 8 ? FrameLength <= 87 : FrameLength <= 118;

    private static bool Bit(int bitmap16, int pgm) => pgm is >= 1 and <= 16 && (bitmap16 & (1 << (pgm - 1))) != 0;

    public IReadOnlyList<int> BypassedZones => Zones.Where(z => z.Status == JflZoneStatus.Bypassed).Select(z => z.Number).ToList();
}
