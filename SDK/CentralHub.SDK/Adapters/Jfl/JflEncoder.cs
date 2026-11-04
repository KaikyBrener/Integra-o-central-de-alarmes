namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>Construtores de quadros enviados pelo servidor (CentralHub) para a central.</summary>
public static class JflEncoder
{
    public const int DefaultKeepAliveMinutes = 5;
    public const int BypassBitmapLength = 13;

    /// <summary>
    /// Resposta ao 0x21: 7B 07 SEQ 21 RESULT KEEPALIVE CS. Ecoa o SEQ da CENTRAL.
    /// RESULT 0x01 aceita; 0x00 rejeita e a central desconecta. Capturado: 7B 07 F9 21 01 05 A0.
    /// </summary>
    public static byte[] ConnectionAck(byte panelSeq, bool accept = true, int keepAliveMinutes = DefaultKeepAliveMinutes) =>
        JflFrame.Build(panelSeq, JflCmd.Connection, new[] { (byte)(accept ? 1 : 0), (byte)keepAliveMinutes });

    /// <summary>Resposta ao keep-alive 0x40 da central: 7B 06 SEQ 40 MINUTOS CS (intervalo de 1 a 20 min).</summary>
    public static byte[] KeepAliveAck(byte panelSeq, int keepAliveMinutes = DefaultKeepAliveMinutes) =>
        JflFrame.Build(panelSeq, JflCmd.KeepAlive, new[] { (byte)keepAliveMinutes });

    /// <summary>
    /// Confirmacao de evento 0x24: 7B 0A SEQ 24 01 CONTADOR[4] CS. O CONTADOR (binario) deve ser
    /// devolvido exatamente como veio; sem a confirmacao a central retransmite para sempre.
    /// </summary>
    public static byte[] EventAck(byte panelSeq, ReadOnlySpan<byte> counter)
    {
        var payload = new byte[5];
        payload[0] = 0x01;
        counter[..Math.Min(4, counter.Length)].CopyTo(payload.AsSpan(1));
        return JflFrame.Build(panelSeq, JflCmd.Event, payload);
    }

    public static byte[] StatusRequest(byte seq) => JflFrame.Build(seq, JflCmd.Status);

    /// <summary>Armar particao (capturado: 7B 06 6F 4E 01 5D).</summary>
    public static byte[] Arm(byte seq, int partition) => JflFrame.Build(seq, JflCmd.Arm, new[] { (byte)partition });

    /// <summary>Desarmar particao (capturado: 7B 06 79 4F 01 4A).</summary>
    public static byte[] Disarm(byte seq, int partition) => JflFrame.Build(seq, JflCmd.Disarm, new[] { (byte)partition });

    /// <summary>Ligar PGM (capturado: 7B 06 45 50 02 6A = PGM 2).</summary>
    public static byte[] PgmOn(byte seq, int pgm) => JflFrame.Build(seq, JflCmd.PgmOn, new[] { (byte)pgm });

    /// <summary>Desligar PGM (capturado: 7B 06 49 51 02 67 = PGM 2).</summary>
    public static byte[] PgmOff(byte seq, int pgm) => JflFrame.Build(seq, JflCmd.PgmOff, new[] { (byte)pgm });

    /// <summary>
    /// Anulacao de zonas: o comando SUBSTITUI o conjunto inteiro (13 bytes, MSB-first: bit 7 do
    /// primeiro byte = zona 1). Passe TODAS as zonas que devem permanecer anuladas.
    /// Capturado (zona 9): 7B 12 4F 52 00 80 00.. F4.
    /// </summary>
    public static byte[] Bypass(byte seq, IEnumerable<int> zones) =>
        JflFrame.Build(seq, JflCmd.Bypass, JflDecoder.NumbersToBitmap(zones, BypassBitmapLength, lsbFirst: false));
}
