namespace CentralHub.SDK.Adapters;

/// <summary>
/// Camada de protocolo JFL linha Active.
///
/// Fonte: engenharia reversa (fernac03/JFL_ACTIVE, Home Assistant Brasil).
/// Protocolo NAO e oficial — requer PDF do fabricante para implementacao completa.
///
/// ATENCAO ARQUITETURAL:
///   A central JFL atua como cliente TCP. O sistema deve ser o servidor TCP.
///   Isso e o OPOSTO do modelo atual (TcpAdapterBase conecta como cliente).
///
///   Para implementar o JflAdapter real, sera necessario:
///   1. Um TcpListener (servidor) escutando em uma porta configuravel.
///   2. Aceitar a conexao da central quando ela ligar.
///   3. Processar os pacotes recebidos (eventos, status).
///   4. Enviar comandos de volta pela mesma conexao ativa.
///
///   Isso implica uma mudanca no JflAdapter que nao usa TcpAdapterBase.
///   TODO: avaliar quando iniciar esta implementacao (requer hardware JFL).
///
/// Estrutura de frame (confirmada pelo codigo fernac03/JFL_ACTIVE):
///   [0x7B][LEN][TIPO][CMD][DATA...][CS]
///   CS = XOR simples de todos os bytes (SEM complemento 0xFF).
///
/// Diferenca critica do checksum:
///   JFL:      CS = XOR(todos os bytes do frame exceto CS)
///   Intelbras: CS = XOR(todos os bytes exceto CS) XOR 0xFF
/// </summary>
internal static class JflActiveProtocol
{
    public const byte StartByte = 0x7B;

    // ----------------------------------------------------------------
    // Frames confirmados pelo codigo fernac03/JFL_ACTIVE
    // ----------------------------------------------------------------

    /// <summary>
    /// ACK enviado ao receber o pacote inicial de conexao da central (5 bytes).
    /// Bytes: 7B 06 01 40 01 [CS]
    /// </summary>
    public static byte[] FrameAckConexao() => ConstruirFrameRaw(0x01, 0x40, new byte[] { 0x01 });

    /// <summary>
    /// Keepalive enviado periodicamente pelo servidor para manter a conexao ativa.
    /// Bytes: 7B 05 02 4D [CS]
    /// </summary>
    public static byte[] FrameKeepalive() => ConstruirFrameRaw(0x02, 0x4D, null);

    /// <summary>
    /// Solicitar status da central.
    /// Bytes: 7B 05 01 4D [CS]
    /// </summary>
    public static byte[] FramePedidoStatus() => ConstruirFrameRaw(0x01, 0x4D, null);

    // ----------------------------------------------------------------
    // Identificacao de modelo (byte[41] no pacote de 102 bytes)
    // Fonte: __init__.py fernac03/JFL_ACTIVE
    // ----------------------------------------------------------------

    public static string IdentificarModelo(byte[] pacote102Bytes)
    {
        if (pacote102Bytes.Length < 42) return "Desconhecido";

        return pacote102Bytes[41] switch
        {
            0xA0 => "Active 32 Duo",
            0xA1 => "Active 20 Ultra/GPRS",
            0xA2 => "Active 8 Ultra",
            0xA3 => "Active 20 Ethernet",
            0xA4 => "Active 100 Bus",
            0xA5 => "Active 20 Bus",
            0xA6 => "Active Full 32",
            0xA7 => "Active 20",
            0xA8 => "Active 8W",
            0x4B => "M-300+",
            0x5D => "M-300 Flex",
            var b => $"Desconhecido (0x{b:X2})"
        };
    }

    // ----------------------------------------------------------------
    // Checksum
    // ----------------------------------------------------------------

    /// <summary>
    /// Calcula o checksum JFL Active.
    /// Formula: XOR simples de todos os bytes (sem complemento).
    /// Diferente do ISECNet que faz XOR 0xFF no final.
    /// </summary>
    public static byte CalcularChecksum(ReadOnlySpan<byte> dados)
    {
        byte xor = 0;
        foreach (var b in dados) xor ^= b;
        return xor;
    }

    // ----------------------------------------------------------------
    // Construcao de frames
    // ----------------------------------------------------------------

    /// <summary>
    /// Constroi um frame JFL: [0x7B][LEN][TIPO][CMD][DATA...][CS]
    /// LEN = 4 + data.Length (inclui TIPO, CMD, DATA, CS)
    /// </summary>
    private static byte[] ConstruirFrameRaw(byte tipo, byte cmd, byte[]? data)
    {
        data ??= Array.Empty<byte>();
        var len = (byte)(3 + data.Length); // TIPO + CMD + DATA + CS (CS incluso no LEN conforme protocolo)
        var frame = new byte[2 + 1 + 1 + data.Length + 1]; // 7B + LEN + TIPO + CMD + DATA + CS
        frame[0] = StartByte;
        frame[1] = len;
        frame[2] = tipo;
        frame[3] = cmd;
        data.CopyTo(frame, 4);
        frame[^1] = CalcularChecksum(frame.AsSpan(0, frame.Length - 1));
        return frame;
    }

    // ----------------------------------------------------------------
    // Comandos confirmados
    // ----------------------------------------------------------------

    /// <summary>Armar particao 1 (away). Bytes: 7B 06 01 4E 01 [CS]</summary>
    public static byte[] FrameArmarParticao1() => ConstruirFrameRaw(0x01, 0x4E, new byte[] { 0x01 });

    /// <summary>Armar particao 2. Bytes: 7B 06 01 4E 02 [CS]</summary>
    public static byte[] FrameArmarParticao2() => ConstruirFrameRaw(0x01, 0x4E, new byte[] { 0x02 });

    /// <summary>Desarmar particao 1. Bytes: 7B 06 01 4F 01 [CS]</summary>
    public static byte[] FrameDesarmarParticao1() => ConstruirFrameRaw(0x01, 0x4F, new byte[] { 0x01 });

    /// <summary>Desarmar particao 2. Bytes: 7B 06 01 4F 02 [CS]</summary>
    public static byte[] FrameDesarmarParticao2() => ConstruirFrameRaw(0x01, 0x4F, new byte[] { 0x02 });

    // ----------------------------------------------------------------
    // Firmware
    //
    // TODO: firmware nao foi encontrado nos bytes do pacote de 102 bytes
    //       analisado. Requer PDF oficial da JFL ou Wireshark com central real.
    //
    // Comandos PGM
    //
    // TODO: nenhum byte de comando PGM encontrado nas fontes publicas.
    //       Requer PDF oficial da JFL.
    //
    // Autenticacao
    //
    // TODO: a autenticacao nao esta documentada nas fontes publicas analisadas.
    //       O codigo fernac03 nao implementa autenticacao por senha.
    // ----------------------------------------------------------------
}
