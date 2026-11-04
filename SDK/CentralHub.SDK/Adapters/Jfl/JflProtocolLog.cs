namespace CentralHub.SDK.Adapters.Jfl;

public enum JflLogDirection
{
    /// <summary>Recebido da central.</summary>
    Rx,
    /// <summary>Enviado para a central.</summary>
    Tx,
    /// <summary>Ocorrencia do servidor (conexao, erro, timeout...).</summary>
    Sistema
}

public enum JflLogKind
{
    Quadro,
    Ack,
    Evento,
    Status,
    Comando,
    Conexao,
    Desconexao,
    Reconexao,
    Timeout,
    Erro
}

public sealed record JflLogEntry(
    long Id,
    DateTime TimestampUtc,
    string? Serial,
    string Remote,
    JflLogDirection Direction,
    JflLogKind Kind,
    byte? Cmd,
    string? Hex,
    string Description);

/// <summary>
/// Registro circular, em memoria, de tudo que passa pelo servidor JFL: quadros RX/TX (hex + interpretacao),
/// confirmacoes, erros, timeouts e (re)conexoes. Existe para diagnostico e para os testes em hardware real.
/// Nao e persistido: reiniciar a API limpa o registro.
/// </summary>
public sealed class JflProtocolLog
{
    private readonly object _gate = new();
    private readonly LinkedList<JflLogEntry> _entries = new();
    private long _nextId;

    public JflProtocolLog(int capacity = 5000) => Capacity = Math.Max(10, capacity);

    public int Capacity { get; }

    public JflLogEntry Add(string? serial, string remote, JflLogDirection direction, JflLogKind kind,
        string description, byte[]? frame = null)
    {
        lock (_gate)
        {
            var entry = new JflLogEntry(
                ++_nextId,
                DateTime.UtcNow,
                serial,
                remote,
                direction,
                kind,
                frame is { Length: > 3 } ? frame[3] : null,
                frame is null ? null : Convert.ToHexString(frame),
                description);
            _entries.AddLast(entry);
            while (_entries.Count > Capacity) _entries.RemoveFirst();
            return entry;
        }
    }

    /// <summary>Entradas em ordem cronologica (a mais antiga primeiro), opcionalmente apos <paramref name="afterId"/>.</summary>
    public IReadOnlyList<JflLogEntry> Snapshot(long afterId = 0)
    {
        lock (_gate)
        {
            return _entries.Where(e => e.Id > afterId).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }

    /// <summary>Registra um quadro com a interpretacao do <see cref="JflFrameDescriber"/>.</summary>
    public void Frame(JflSession session, JflFrame frame, JflLogDirection direction)
    {
        var (kind, text) = JflFrameDescriber.Describe(frame, direction);
        var serial = session.Serial;
        if (serial is null && direction == JflLogDirection.Rx && frame.Cmd == JflCmd.Connection
            && frame.Length >= JflDecoder.ConnectionFrameLength)
        {
            // O quadro 0x21 chega antes de a sessao estar identificada: a serie vem dele mesmo.
            serial = JflDecoder.DecodeConnection(frame).Serial;
        }
        Add(serial, session.RemoteEndPoint, direction, kind, text, frame.Raw);
    }
}

/// <summary>Traduz um quadro JFL para uma frase legivel (usada no log de protocolo).</summary>
public static class JflFrameDescriber
{
    public static (JflLogKind Kind, string Text) Describe(JflFrame frame, JflLogDirection direction)
    {
        var rx = direction == JflLogDirection.Rx;
        try
        {
            switch (frame.Cmd)
            {
                case JflCmd.Connection when rx && frame.Length >= JflDecoder.ConnectionFrameLength:
                {
                    var info = JflDecoder.DecodeConnection(frame);
                    var profile = JflModelProfile.Resolve(info.ModelByte);
                    return (JflLogKind.Conexao,
                        $"0x21 Conexao: serie {info.Serial}, modelo 0x{info.ModelByte:X2} ({profile.Name}), firmware {info.Firmware}, MAC {info.Mac}");
                }
                case JflCmd.Connection when !rx:
                    return (JflLogKind.Ack, frame.Payload.Length > 1
                        ? $"ACK de conexao: {(frame.Payload[0] == 1 ? "aceita" : "RECUSADA")}, keep-alive {frame.Payload[1]} min"
                        : "ACK de conexao");

                case JflCmd.Event when rx && frame.Length >= 22:
                {
                    var e = JflDecoder.DecodeEvent(frame);
                    var entry = JflContactIdTable.Find(e.Code);
                    return (JflLogKind.Evento,
                        $"0x24 Evento {e.Code} {entry?.Description ?? "(nao catalogado)"} - conta {e.Account}, particao {e.Partition}, zona/usuario {e.Subject}, contador {Convert.ToHexString(e.Counter)}");
                }
                case JflCmd.Event when !rx:
                    return (JflLogKind.Ack, $"ACK de evento (contador {Convert.ToHexString(frame.Payload[1..])})");

                case JflCmd.KeepAlive when rx:
                    return (JflLogKind.Quadro, "0x40 Keep-alive da central");
                case JflCmd.KeepAlive:
                    return (JflLogKind.Ack, $"ACK de keep-alive ({(frame.Payload.Length > 0 ? frame.Payload[0] : 0)} min)");

                case JflCmd.Status or JflCmd.StatusUser when !rx:
                    return (JflLogKind.Comando, $"0x{frame.Cmd:X2} Pedido de status");
                case JflCmd.Arm when !rx:
                    return (JflLogKind.Comando, $"0x4E Armar particao {frame.Payload[0]}");
                case JflCmd.Disarm when !rx:
                    return (JflLogKind.Comando, $"0x4F Desarmar particao {frame.Payload[0]}");
                case JflCmd.PgmOn when !rx:
                    return (JflLogKind.Comando, $"0x50 Ligar PGM {frame.Payload[0]}");
                case JflCmd.PgmOff when !rx:
                    return (JflLogKind.Comando, $"0x51 Desligar PGM {frame.Payload[0]}");
                case JflCmd.Bypass when !rx:
                {
                    var zones = JflDecoder.BitmapToNumbers(frame.Payload, lsbFirst: false);
                    return (JflLogKind.Comando, zones.Count == 0
                        ? "0x52 Anulacao: nenhuma zona anulada"
                        : $"0x52 Anulacao: zonas {string.Join(", ", zones)}");
                }

                default:
                    if (rx && JflCmd.RepliesWithStatus(frame.Cmd) && frame.Length >= JflDecoder.StatusMinLength)
                    {
                        var s = JflDecoder.DecodeStatus(frame);
                        var parts = s.Partitions.Select((p, i) => (p, i)).Where(x => x.p.Programmed)
                            .Select(x => $"P{x.i + 1}={(x.p.Armed ? (x.p.ArmedStay ? "stay" : "armada") : "desarmada")}{(x.p.InAlarm ? "!" : "")}");
                        var pgms = Enumerable.Range(1, 16).Where(s.IsPgmOn).ToList();
                        return (JflLogKind.Status,
                            $"Status (eco 0x{frame.Cmd:X2}, {frame.Length} bytes): {string.Join(" ", parts)}; " +
                            $"PGMs ligadas: {(pgms.Count == 0 ? "nenhuma" : string.Join(",", pgms))}; " +
                            $"zonas abertas: {s.Zones.Count(z => z.Status == JflZoneStatus.Open)}, anuladas: {s.BypassedZones.Count}; " +
                            $"bateria {s.BatteryVolts:0.0} V; relogio {s.Clock}");
                    }
                    return (JflLogKind.Quadro, $"Quadro 0x{frame.Cmd:X2} nao interpretado ({frame.Length} bytes)");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            return (JflLogKind.Erro, $"Quadro 0x{frame.Cmd:X2} nao decodificado: {ex.Message}");
        }
    }
}
