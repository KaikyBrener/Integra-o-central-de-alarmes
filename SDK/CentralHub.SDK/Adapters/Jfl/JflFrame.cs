namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>
/// Constantes do quadro JFL "0x7B" (monitoramento IP das centrais Active).
///
/// Estrutura (confirmada em capturas reais de uma Active 32 Duo fw 7.60 — ver
/// Tests/Fixtures — e no PDF de comandos da JFL citado por jmceara/pyjfl):
///
///   [0x7B][TAM][SEQ][CMD][DADOS...][CS]
///
///   TAM = tamanho TOTAL do quadro (inclui 0x7B, TAM, SEQ, CMD, DADOS e CS).
///   CS  = XOR de todos os bytes anteriores; um quadro valido tem XOR total == 0.
///
/// ATENCAO: existem outras geracoes de protocolo JFL (cabecalho 0xB3 para Active 32 Duo
/// fw &lt; 5.0, e 0x7A com tamanho em 2 bytes para Active 8W/QC/Vulcano). Elas NAO sao
/// implementadas aqui; o leitor sinaliza o caso em vez de interpreta-las errado.
/// </summary>
public static class JflProtocol
{
    public const byte Header = 0x7B;
    public const byte LegacyHeader = 0xB3;
    public const byte WirelessHeader = 0x7A;
    public const int MinFrameLength = 5;
    public const int MaxFrameLength = 255;
}

/// <summary>Quadro JFL completo e com checksum valido.</summary>
public sealed class JflFrame
{
    public JflFrame(byte[] raw)
    {
        if (!IsValid(raw))
        {
            throw new ArgumentException("Quadro JFL invalido (cabecalho, tamanho ou checksum).", nameof(raw));
        }
        Raw = raw;
    }

    public byte[] Raw { get; }
    public byte Seq => Raw[2];
    public byte Cmd => Raw[3];
    public int Length => Raw.Length;
    public ReadOnlySpan<byte> Payload => Raw.AsSpan(4, Raw.Length - 5);

    /// <summary>Byte no deslocamento ABSOLUTO (contado a partir do 0x7B), como nos PDFs da JFL.</summary>
    public byte ByteAt(int offset) => Raw[offset];

    public ReadOnlySpan<byte> Slice(int start, int endExclusive) => Raw.AsSpan(start, endExclusive - start);

    public static byte Xor(ReadOnlySpan<byte> data)
    {
        byte x = 0;
        foreach (var b in data) x ^= b;
        return x;
    }

    public static bool IsValid(ReadOnlySpan<byte> frame) =>
        frame.Length >= JflProtocol.MinFrameLength
        && frame.Length <= JflProtocol.MaxFrameLength
        && frame[0] == JflProtocol.Header
        && frame[1] == frame.Length
        && Xor(frame) == 0;

    /// <summary>Monta um quadro. SEQ 0x00 e proibido pela especificacao e nunca e emitido.</summary>
    public static byte[] Build(byte seq, byte cmd, ReadOnlySpan<byte> payload = default)
    {
        var total = JflProtocol.MinFrameLength + payload.Length;
        if (total > JflProtocol.MaxFrameLength)
        {
            throw new ArgumentException($"Quadro JFL teria {total} bytes (maximo {JflProtocol.MaxFrameLength}).");
        }

        var frame = new byte[total];
        frame[0] = JflProtocol.Header;
        frame[1] = (byte)total;
        frame[2] = seq == 0 ? (byte)1 : seq;
        frame[3] = cmd;
        payload.CopyTo(frame.AsSpan(4));
        frame[^1] = Xor(frame.AsSpan(0, total - 1));
        return frame;
    }

    public override string ToString() => Convert.ToHexString(Raw);
}

/// <summary>Gerador de bytes de sequencia 0x01..0xFF (nunca 0x00).</summary>
public sealed class JflSequence
{
    private int _next;

    public JflSequence(byte start = 1) => _next = start == 0 ? 1 : start;

    public byte Next()
    {
        var value = (byte)_next;
        _next = _next >= 0xFF ? 1 : _next + 1;
        return value;
    }
}

/// <summary>Geracao de protocolo detectada no primeiro byte do fluxo e nao suportada.</summary>
public enum JflUnsupportedGeneration
{
    None,
    Legacy0xB3,
    Wireless0x7A
}

/// <summary>
/// Remonta quadros a partir do fluxo TCP. TCP junta e divide leituras arbitrariamente, por isso
/// o despacho e sempre por CMD e nunca pelo tamanho de uma leitura. Em caso de checksum invalido
/// descarta UM byte (0x7B tambem aparece em contas/IMEI/Contact ID) e ressincroniza.
/// </summary>
public sealed class JflFrameReader
{
    private const int MaxBuffer = JflProtocol.MaxFrameLength * 4;

    private readonly List<byte> _buffer = new();
    private bool _firstByteChecked;

    public int DroppedBytes { get; private set; }
    public int Desyncs { get; private set; }
    public JflUnsupportedGeneration UnsupportedGeneration { get; private set; }
    public int Pending => _buffer.Count;

    public void Reset()
    {
        _buffer.Clear();
        _firstByteChecked = false;
        UnsupportedGeneration = JflUnsupportedGeneration.None;
    }

    public IReadOnlyList<JflFrame> Feed(ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty && !_firstByteChecked)
        {
            _firstByteChecked = true;
            if (data[0] == JflProtocol.LegacyHeader) UnsupportedGeneration = JflUnsupportedGeneration.Legacy0xB3;
            else if (data[0] == JflProtocol.WirelessHeader) UnsupportedGeneration = JflUnsupportedGeneration.Wireless0x7A;
        }

        foreach (var b in data) _buffer.Add(b);

        var frames = new List<JflFrame>();
        while (TakeOne() is { } frame)
        {
            frames.Add(frame);
        }

        if (_buffer.Count > MaxBuffer)
        {
            Desyncs++;
            DroppedBytes += _buffer.Count;
            _buffer.Clear();
        }

        return frames;
    }

    private JflFrame? TakeOne()
    {
        while (true)
        {
            var start = _buffer.IndexOf(JflProtocol.Header);
            if (start < 0)
            {
                DroppedBytes += _buffer.Count;
                _buffer.Clear();
                return null;
            }

            if (start > 0)
            {
                DroppedBytes += start;
                _buffer.RemoveRange(0, start);
            }

            if (_buffer.Count < 2) return null;

            int length = _buffer[1];
            if (length < JflProtocol.MinFrameLength)
            {
                DropOne();
                continue;
            }

            if (_buffer.Count < length)
            {
                // Candidato incompleto. Normalmente basta esperar; mas um '{' dentro de um payload
                // seguido de byte grande simula um quadro longo e travaria os reais logo atras.
                // Se ja existe um quadro completo e valido adiante, o candidato era fantasma.
                var real = ScanForValidFrame();
                if (real is null) return null;
                DroppedBytes += real.Value;
                _buffer.RemoveRange(0, real.Value);
                continue;
            }

            var candidate = _buffer.GetRange(0, length).ToArray();
            if (JflFrame.Xor(candidate) == 0)
            {
                _buffer.RemoveRange(0, length);
                return new JflFrame(candidate);
            }

            DropOne();
        }
    }

    private int? ScanForValidFrame()
    {
        var offset = 1;
        while (true)
        {
            offset = _buffer.IndexOf(JflProtocol.Header, offset);
            if (offset < 0) return null;

            var length = offset + 1 < _buffer.Count ? _buffer[offset + 1] : 0;
            if (length >= JflProtocol.MinFrameLength
                && length <= _buffer.Count - offset
                && JflFrame.Xor(_buffer.GetRange(offset, length).ToArray()) == 0)
            {
                return offset;
            }

            offset++;
        }
    }

    private void DropOne()
    {
        DroppedBytes++;
        _buffer.RemoveAt(0);
    }
}
