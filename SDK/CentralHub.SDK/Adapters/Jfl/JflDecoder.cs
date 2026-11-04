using System.Text;

namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>
/// Decodifica quadros JFL em registros tipados. Todos os deslocamentos sao ABSOLUTOS (contados a
/// partir do 0x7B), exatamente como os PDFs da JFL numeram os campos. O despacho e por CMD, nunca
/// pelo tamanho da leitura.
///
/// Fontes: PDF de comandos da JFL (citado por jmceara/pyjfl) e capturas de uma Active 32 Duo fw 7.60.
/// O layout e o mesmo documento para Active 100/32/20 Bus/20/8/20 Eth, mas SO a 32 Duo foi
/// validada em hardware; para 100 Bus e Active 20 os campos sao "pela especificacao".
/// </summary>
public static class JflDecoder
{
    public const int ConnectionFrameLength = 102;
    public const int StatusMinLength = 101; // o status da fw 7.60 tem 127 bytes (documentado: 123)

    public static int BcdToInt(byte b) => (b >> 4) * 10 + (b & 0x0F);

    public static string Text(ReadOnlySpan<byte> data)
    {
        var chars = new List<byte>();
        foreach (var b in data)
        {
            if (b != 0xFF && b != 0x00) chars.Add(b);
        }
        return Encoding.Latin1.GetString(chars.ToArray()).Trim();
    }

    /// <summary>
    /// Quadro 0x21 (102 bytes): 4-13 serie; 14-28 IMEI; 29-40 MAC; 41 modelo; 42-44 versao;
    /// 49 sinal; 51 numero de particoes; 84 eletrificador; 85-100 estado das 16 particoes.
    /// </summary>
    public static JflConnectionInfo DecodeConnection(JflFrame frame)
    {
        var partitions = new List<JflPartitionState>();
        byte fence = 0;
        if (frame.Length > 100)
        {
            fence = frame.ByteAt(84);
            foreach (var b in frame.Slice(85, 101)) partitions.Add(new JflPartitionState(b));
        }

        return new JflConnectionInfo(
            Serial: Text(frame.Slice(4, 14)),
            ModelByte: frame.ByteAt(41),
            Firmware: Text(frame.Slice(42, 45)),
            Mac: Text(frame.Slice(29, 41)),
            Imei: Text(frame.Slice(14, 29)),
            PartitionCount: frame.Length > 51 ? frame.ByteAt(51) : 0,
            Signal: frame.Length > 49 ? frame.ByteAt(49) : 0,
            FenceRaw: fence,
            Partitions: partitions);
    }

    /// <summary>Quadro 0x24 (24 bytes): 4-7 conta; 8-11 evento (Q+XYZ); 12-13 particao; 14-16 usuario/zona; 17-20 contador.</summary>
    public static JflPanelEvent DecodeEvent(JflFrame frame) => new(
        Account: Text(frame.Slice(4, 8)),
        Code: Text(frame.Slice(8, 12)),
        Partition: Text(frame.Slice(12, 14)),
        Subject: Text(frame.Slice(14, 17)),
        Counter: frame.Slice(17, 21).ToArray(),
        FenceRaw: frame.Length > 22 ? frame.ByteAt(21) : (byte)0,
        Problem: frame.Length > 23 ? frame.ByteAt(22) : (byte)0,
        Seq: frame.Seq);

    /// <summary>
    /// Quadro de status. Tres ordens de bit diferentes convivem aqui:
    ///  - PGM (13), PGM2 (117), P-PGM (87), P-PGM2 (118): LSB = PGM 1;
    ///  - P-INIB (104-116): LSB = menor zona (contrario ao que a especificacao afirma);
    ///  - ZONA (31-80): dois nibbles por byte, nibble alto = zona de menor numero.
    /// </summary>
    public static JflStatus DecodeStatus(JflFrame frame)
    {
        var size = frame.Length;
        if (size < StatusMinLength)
        {
            throw new ArgumentException($"Quadro de status curto demais ({size} bytes).", nameof(frame));
        }

        var bypassable = new List<int>();
        if (size > 117)
        {
            bypassable.AddRange(BitmapToNumbers(frame.Slice(104, 117), lsbFirst: true));
        }

        var bypassableSet = bypassable.ToHashSet();
        var zones = new List<JflZone>();
        var zoneBlock = frame.Slice(31, 81);
        for (var i = 0; i < zoneBlock.Length; i++)
        {
            for (var half = 0; half < 2; half++)
            {
                var nibble = half == 0 ? (zoneBlock[i] >> 4) & 0x0F : zoneBlock[i] & 0x0F;
                var number = i * 2 + half + 1;
                // Nibble nao documentado e tratado como "desabilitada": um firmware novo nao pode derrubar o status.
                var status = Enum.IsDefined(typeof(JflZoneStatus), nibble) ? (JflZoneStatus)nibble : JflZoneStatus.Disabled;
                if (status != JflZoneStatus.Disabled)
                {
                    zones.Add(new JflZone(number, status, bypassableSet.Contains(number)));
                }
            }
        }

        var problems = new List<JflProblem>();
        var problemBytes = frame.Slice(81, 86);
        for (var i = 0; i < problemBytes.Length; i++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                if ((problemBytes[i] & (1 << bit)) != 0 && Enum.IsDefined(typeof(JflProblem), i * 8 + bit))
                {
                    problems.Add((JflProblem)(i * 8 + bit));
                }
            }
        }

        var pgmState = frame.ByteAt(13) | ((size > 117 ? frame.ByteAt(117) : 0) << 8);
        var pgmPermissions = (size > 87 ? frame.ByteAt(87) : 0) | ((size > 118 ? frame.ByteAt(118) : 0) << 8);

        return new JflStatus(
            Clock: DecodeClock(frame.Slice(6, 12)),
            BatteryVolts: frame.ByteAt(12) / 14.0,
            PgmState: pgmState,
            Partitions: frame.Slice(14, 30).ToArray().Select(b => new JflPartitionState(b)).ToList(),
            FenceRaw: frame.ByteAt(30),
            Zones: zones,
            Problems: problems,
            PgmPermissions: pgmPermissions,
            BypassableZones: bypassable,
            FrameLength: size,
            Seq: frame.Seq);
    }

    /// <summary>Campo HORA: DIA MES ANO HORA MIN SEG em BCD (dia primeiro).</summary>
    public static string DecodeClock(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) return string.Empty;
        return $"{BcdToInt(data[0]):00}/{BcdToInt(data[1]):00}/{BcdToInt(data[2]):00} " +
               $"{BcdToInt(data[3]):00}:{BcdToInt(data[4]):00}:{BcdToInt(data[5]):00}";
    }

    public static IReadOnlyList<int> BitmapToNumbers(ReadOnlySpan<byte> data, bool lsbFirst)
    {
        var numbers = new List<int>();
        for (var i = 0; i < data.Length; i++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = lsbFirst ? 1 << bit : 0x80 >> bit;
                if ((data[i] & mask) != 0) numbers.Add(i * 8 + bit + 1);
            }
        }
        return numbers;
    }

    public static byte[] NumbersToBitmap(IEnumerable<int> numbers, int length, bool lsbFirst)
    {
        var buffer = new byte[length];
        foreach (var n in numbers)
        {
            var index = (n - 1) / 8;
            var bit = (n - 1) % 8;
            if (n >= 1 && index < length)
            {
                buffer[index] |= (byte)(lsbFirst ? 1 << bit : 0x80 >> bit);
            }
        }
        return buffer;
    }
}
