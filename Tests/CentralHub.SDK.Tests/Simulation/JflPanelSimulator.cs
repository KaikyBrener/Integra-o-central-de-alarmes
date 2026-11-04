using System.Net.Sockets;
using System.Text;
using CentralHub.SDK.Adapters.Jfl;
using CentralHub.SDK.Tests.Jfl;

namespace CentralHub.SDK.Tests.Simulation;

/// <summary>
/// SIMULADOR de central JFL, usado SOMENTE para testar o protocolo (servidor, adapter, reconexao,
/// confirmacoes). NAO e captura real e NAO prova compatibilidade com nenhum equipamento.
///
/// Os quadros 0x21 e de status sao derivados de capturas reais da Active 32 Duo (RealCaptures),
/// alterando apenas os campos de dados (serie, modelo, particoes, zonas, PGMs) e recalculando o
/// checksum. O comportamento (responder a status/arme/PGM/bypass com um quadro de status que ecoa
/// CMD e SEQ; so os comandos sem autenticacao) segue o observado na Active 32 Duo.
/// </summary>
public sealed class JflPanelSimulator : IAsyncDisposable
{
    private readonly int _serverPort;
    private readonly object _gate = new();
    private readonly List<JflFrame> _received = new();
    private readonly JflFrameReader _reader = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public JflPanelSimulator(int serverPort, string serial, byte modelByte, string firmware = "700")
    {
        if (serial.Length != 10) throw new ArgumentException("Serie de 10 caracteres.", nameof(serial));
        _serverPort = serverPort;
        Serial = serial;
        ModelByte = modelByte;
        Firmware = firmware;
    }

    public string Serial { get; }
    public byte ModelByte { get; }
    public string Firmware { get; }

    // ---- estado simulado da central ----
    public byte[] Partitions { get; } = new byte[16];
    public int PgmState { get; set; }
    public int PgmPermitted { get; set; }
    public Dictionary<int, byte> Zones { get; } = new();          // numero -> nibble (7 aberta, 8 fechada...)
    public HashSet<int> Bypassable { get; } = new();
    public HashSet<int> Bypassed { get; } = new();
    public bool RefuseArm { get; set; }
    public bool Mute { get; set; }                                  // nao responde a comandos

    public bool ConnectionAccepted { get; private set; }
    public bool Connected => _client?.Connected == true;

    public IReadOnlyList<JflFrame> Received { get { lock (_gate) return _received.ToList(); } }

    // ---- fabricas dos dois modelos prioritarios (tetos do datasheet oficial) ----

    public static JflPanelSimulator Active100Bus(int port, string serial = "1000000001")
    {
        var sim = new JflPanelSimulator(port, serial, JflModelProfile.ByteActive100Bus);
        for (var i = 0; i < 16; i++) sim.Partitions[i] = 0x01;
        for (var z = 1; z <= 99; z++) { sim.Zones[z] = 8; sim.Bypassable.Add(z); }
        sim.PgmPermitted = 0xFFFF;
        return sim;
    }

    public static JflPanelSimulator Active20(int port, string serial = "2000000001")
    {
        var sim = new JflPanelSimulator(port, serial, JflModelProfile.ByteActive20);
        sim.Partitions[0] = 0x01; sim.Partitions[1] = 0x01;
        for (var z = 1; z <= 32; z++) { sim.Zones[z] = 8; sim.Bypassable.Add(z); }
        sim.PgmPermitted = 0x000F;
        return sim;
    }

    // ---- ciclo de vida ----

    public async Task ConnectAsync(byte panelSeq = 0xF9)
    {
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync("127.0.0.1", _serverPort);
        _stream = _client.GetStream();
        _cts = new CancellationTokenSource();

        await _stream.WriteAsync(BuildConnectionFrame(panelSeq));
        _loop = Task.Run(() => ReadLoopAsync(_cts.Token));

        var ack = await WaitForFrameAsync(f => f.Cmd == JflCmd.Connection);
        ConnectionAccepted = ack.Payload.Length >= 1 && ack.Payload[0] == 0x01;
    }

    /// <summary>Abre a conexao TCP sem enviar o quadro 0x21 (para testar entradas invalidas).</summary>
    public async Task ConnectNoHandshakeAsync()
    {
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync("127.0.0.1", _serverPort);
        _stream = _client.GetStream();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    public async Task SendRawAsync(byte[] bytes) => await _stream!.WriteAsync(bytes);

    public async Task SendEventAsync(string code, int partition, int subject, byte seq = 0x52, string account = "0001",
        byte[]? counter = null)
    {
        counter ??= new byte[] { 0x00, 0x00, 0x39, 0x1D };
        var payload = Encoding.ASCII.GetBytes($"{account}{code}{partition:00}{subject:000}")
            .Concat(counter).Concat(new byte[] { 0x02, 0x00 }).ToArray();
        await SendRawAsync(JflFrame.Build(seq, JflCmd.Event, payload));
    }

    public async Task SendKeepAliveAsync(byte seq = 0x07) => await SendRawAsync(JflFrame.Build(seq, JflCmd.KeepAlive));

    public async Task<JflFrame> WaitForFrameAsync(Func<JflFrame, bool> predicate, int timeoutMs = 5000, int skip = 0)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate)
            {
                var found = _received.Where(predicate).Skip(skip).FirstOrDefault();
                if (found is not null) return found;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("O simulador nao recebeu o quadro esperado.");
    }

    public int Count(Func<JflFrame, bool> predicate)
    {
        lock (_gate) return _received.Count(predicate);
    }

    /// <summary>Verdadeiro quando o servidor encerrou a conexao.</summary>
    public async Task<bool> WaitForCloseAsync(int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_loop?.IsCompleted == true) return true;
            await Task.Delay(20);
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        try { _client?.Close(); } catch { /* ja fechado */ }
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* melhor esforco */ }
        }
    }

    // ---- comportamento da central ----

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await _stream!.ReadAsync(buffer, ct);
                if (n == 0) return;
                foreach (var frame in _reader.Feed(buffer.AsSpan(0, n)))
                {
                    lock (_gate) _received.Add(frame);
                    await HandleAsync(frame);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or SocketException)
        {
            // conexao encerrada
        }
    }

    private async Task HandleAsync(JflFrame frame)
    {
        if (Mute) return;

        switch (frame.Cmd)
        {
            case JflCmd.Status:
            case JflCmd.StatusUser:
                break;
            case JflCmd.Arm:
            {
                var p = frame.Payload[0];
                if (!RefuseArm && p >= 1 && p <= 16 && Partitions[p - 1] != 0) Partitions[p - 1] = 0x02;
                break;
            }
            case JflCmd.Disarm:
            {
                var p = frame.Payload[0];
                if (p >= 1 && p <= 16 && Partitions[p - 1] != 0) Partitions[p - 1] = 0x01;
                break;
            }
            case JflCmd.PgmOn:
            case JflCmd.PgmOff:
            {
                var n = frame.Payload[0];
                if (n >= 1 && n <= 16 && (PgmPermitted & (1 << (n - 1))) != 0)
                {
                    if (frame.Cmd == JflCmd.PgmOn) PgmState |= 1 << (n - 1); else PgmState &= ~(1 << (n - 1));
                }
                break;
            }
            case JflCmd.Bypass:
            {
                var requested = JflDecoder.BitmapToNumbers(frame.Payload, lsbFirst: false).Where(Bypassable.Contains).ToHashSet();
                Bypassed.Clear();
                foreach (var z in requested) Bypassed.Add(z);
                break;
            }
            default:
                return; // acks do servidor (0x21, 0x24, 0x40) e demais: nada a responder
        }

        await _stream!.WriteAsync(BuildStatusFrame(frame.Seq, frame.Cmd));
    }

    // ---- construcao de quadros a partir de capturas reais ----

    private byte[] BuildConnectionFrame(byte seq)
    {
        var raw = RealCaptures.Bytes(RealCaptures.Connection);
        Encoding.ASCII.GetBytes(Serial).CopyTo(raw, 4);
        raw[2] = seq;
        raw[41] = ModelByte;
        Encoding.ASCII.GetBytes(Firmware.PadRight(3).Substring(0, 3)).CopyTo(raw, 42);
        raw[51] = (byte)Partitions.Count(p => p != 0);
        for (var i = 0; i < 16; i++) raw[85 + i] = Partitions[i];
        return Reseal(raw);
    }

    private byte[] BuildStatusFrame(byte seq, byte cmd)
    {
        var raw = RealCaptures.Bytes(RealCaptures.StatusAfterPgmOn);
        raw[2] = seq;
        raw[3] = cmd;
        raw[13] = (byte)(PgmState & 0xFF);
        raw[117] = (byte)((PgmState >> 8) & 0xFF);
        raw[87] = (byte)(PgmPermitted & 0xFF);
        raw[118] = (byte)((PgmPermitted >> 8) & 0xFF);
        for (var i = 0; i < 16; i++) raw[14 + i] = Partitions[i];

        Array.Clear(raw, 31, 50);
        foreach (var (number, nibble) in Zones)
        {
            var value = Bypassed.Contains(number) ? (byte)JflZoneStatus.Bypassed : nibble;
            var index = 31 + (number - 1) / 2;
            raw[index] |= (number % 2 == 1) ? (byte)(value << 4) : value;
        }

        Array.Clear(raw, 104, 13);
        var inib = JflDecoder.NumbersToBitmap(Bypassable, 13, lsbFirst: true);
        inib.CopyTo(raw, 104);

        return Reseal(raw);
    }

    private static byte[] Reseal(byte[] raw)
    {
        raw[^1] = 0;
        raw[^1] = JflFrame.Xor(raw);
        return raw;
    }
}
