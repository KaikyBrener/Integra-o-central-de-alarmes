namespace CentralHub.SDK.Adapters;

/// <summary>
/// Camada de protocolo ISECNet V2 (Intelbras AMT).
///
/// Documentacao confirmada via engenharia reversa (elvis-epx/alarme-intelbras)
/// com confirmacao do suporte tecnico Intelbras (sem NDA).
///
/// Estrutura de frame:
///   [LEN (1B)] [DATA (LEN bytes)] [CS (1B)]
///   LEN = numero de bytes de DATA (nao inclui LEN nem CS)
///   CS  = XOR(LEN_byte + todos_os_bytes_de_DATA) XOR 0xFF
///
/// Verificacao matematica (frames capturados):
///   02:e0:01:1c -> XOR(02,e0,01)^FF = e3^FF = 1c OK
///   06:e3:45:30:31:30:34:5a -> XOR(06,e3,45,30,31,30,34)^FF = a5^FF = 5a OK
///   08:e9:21:31:32:33:34:5a:21:40 -> XOR(08..21)^FF = bf^FF = 40 OK
/// </summary>
internal static class IsecNetProtocol
{
    // ----------------------------------------------------------------
    // Opcodes confirmados pelo codigo-fonte elvis-epx/alarme-intelbras
    // ----------------------------------------------------------------

    /// <summary>Solicitar status da central. Resposta contem modelo e firmware.</summary>
    public const int OpcodeStatus = 0x0b4a;

    /// <summary>Armar / desarmar particao. Payload: [particao, 0x01=armar | 0x00=desarmar].</summary>
    public const int OpcodeArmDesarm = 0x401e;

    /// <summary>Desligar sirene. Payload: [particao].</summary>
    public const int OpcodeDesligarSirene = 0x4019;

    /// <summary>Bypass / cancel bypass de zona. Payload: [zona-1, 0x01=bypass | 0x00=cancel].</summary>
    public const int OpcodeBypassZona = 0x401f;

    /// <summary>Limpar registro de disparo.</summary>
    public const int OpcodeLimparDisparo = 0x4013;

    /// <summary>Resposta generica de sucesso da central.</summary>
    public const int OpcodeRespostaSucesso = 0xf0fe;

    // ----------------------------------------------------------------
    // Identificacao de modelo (byte[1] da resposta ao OpcodeStatus)
    // Fonte: comandos.go elvis-epx
    // ----------------------------------------------------------------

    public const byte ModeloAmt8000 = 0x01;

    // ----------------------------------------------------------------
    // Checksum
    // ----------------------------------------------------------------

    /// <summary>
    /// Calcula o checksum ISECNet V2.
    /// Cobre todos os bytes passados (deve incluir o LEN byte + DATA bytes).
    /// Formula: XOR(todos os bytes) XOR 0xFF
    /// </summary>
    public static byte CalcularChecksum(ReadOnlySpan<byte> dados)
    {
        byte xor = 0;
        foreach (var b in dados) xor ^= b;
        return (byte)(xor ^ 0xFF);
    }

    // ----------------------------------------------------------------
    // Construcao de frames
    // ----------------------------------------------------------------

    /// <summary>
    /// Constroi um frame ISECNet a partir de um opcode de 2 bytes e um payload opcional.
    /// Frame resultante: [LEN][opcode_hi][opcode_lo][payload...][CS]
    /// </summary>
    public static byte[] ConstruirFrame(int opcode, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();

        // DATA = [opcode_hi, opcode_lo, ...payload]
        var data = new byte[2 + payload.Length];
        data[0] = (byte)((opcode >> 8) & 0xFF);
        data[1] = (byte)(opcode & 0xFF);
        payload.CopyTo(data, 2);

        // Frame = [LEN][DATA...][CS]
        var frame = new byte[1 + data.Length + 1];
        frame[0] = (byte)data.Length; // LEN
        data.CopyTo(frame, 1);

        // CS cobre LEN + DATA (frame sem o ultimo byte)
        frame[^1] = CalcularChecksum(frame.AsSpan(0, frame.Length - 1));

        return frame;
    }

    /// <summary>
    /// Valida o checksum de um frame recebido.
    /// </summary>
    public static bool ValidarChecksum(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3) return false;
        var cs = CalcularChecksum(frame[..^1]); // tudo exceto o ultimo byte
        return cs == frame[^1];
    }

    // ----------------------------------------------------------------
    // Frames de handshake (conexao via Receptor IP Intelbras)
    // Bytes extraidos dos traces oficiais da documentacao Intelbras.
    //
    // TODO: validar se conexao DIRETA a central (sem Receptor IP) usa
    //       o mesmo handshake ou fluxo diferente.
    // ----------------------------------------------------------------

    /// <summary>
    /// Frame de saudacao inicial (passo 1 do handshake com Receptor IP).
    /// Bytes do trace: 02:e0:01:1c
    /// O Receptor IP ecoa o mesmo frame de volta.
    /// </summary>
    public static readonly byte[] FrameHandshake1 = { 0x02, 0xe0, 0x01, 0x1c };

    /// <summary>
    /// Frame de identificacao (passo 2 do handshake com Receptor IP).
    /// Bytes do trace: 06:e3:45:30:31:30:34:5a
    /// Contem codigo de conta ASCII "E0104" (bytes 45:30:31:30:34).
    /// TODO: confirmar se o codigo de conta e fixo ou parametrizavel.
    /// </summary>
    public static readonly byte[] FrameHandshake2 = { 0x06, 0xe3, 0x45, 0x30, 0x31, 0x30, 0x34, 0x5a };

    /// <summary>
    /// Frame de confirmacao (passo 3 do handshake com Receptor IP).
    /// Bytes do trace: 06:e4:45:30:31:30:34:5d
    /// </summary>
    public static readonly byte[] FrameHandshake3 = { 0x06, 0xe4, 0x45, 0x30, 0x31, 0x30, 0x34, 0x5d };

    // ----------------------------------------------------------------
    // Parser de resposta de status (OpcodeStatus = 0x0b4a)
    // Fonte: comandos.go elvis-epx (funcao RespostaStatus)
    // ----------------------------------------------------------------

    public static (string modelo, string firmware) ParsearRespostaStatus(byte[] payload)
    {
        // payload[0] e prefixado com 0x00 no codigo de referencia (slice ajustado aqui)
        if (payload.Length < 5)
            return ("Desconhecido", "Desconhecido");

        var modeloCodigo = payload[1];
        var modelo = modeloCodigo == ModeloAmt8000 ? "AMT-8000" : $"Desconhecido (0x{modeloCodigo:X2})";
        var firmware = $"{payload[2]}.{payload[3]}.{payload[4]}";

        return (modelo, firmware);
    }

    // ----------------------------------------------------------------
    // Autenticacao direta na central (sem Receptor IP)
    // Fonte: goalarmeitbl/protocol.go (PacoteIsecNet2Auth e ContactIDEncode)
    // ----------------------------------------------------------------

    /// <summary>
    /// Codifica um numero no formato Contact ID. O digito '0' e mapeado para 0x0a.
    /// Ex: senha 1234 -> [0x01, 0x02, 0x03, 0x04].
    /// </summary>
    private static byte[] ContactIdEncode(string numeroStr, int tamanhoEsperado)
    {
        var dados = new byte[tamanhoEsperado];
        var span = numeroStr.AsSpan();
        
        // Preenche da direita para a esquerda. Numeros menores que o tamanho esperado
        // terao zeros a esquerda (0x0a).
        for (int i = 0; i < tamanhoEsperado; i++)
        {
            int stringIndex = span.Length - 1 - i;
            if (stringIndex >= 0 && int.TryParse(span.Slice(stringIndex, 1), out int digito))
            {
                dados[tamanhoEsperado - 1 - i] = (byte)(digito == 0 ? 0x0a : digito);
            }
            else
            {
                dados[tamanhoEsperado - 1 - i] = 0x0a; // pad left com zero (0x0a)
            }
        }
        return dados;
    }

    /// <summary>
    /// Constroi o frame de autenticacao (Opcode 0xf0f0).
    /// Payload: [sw_type(1B)][senha_contact_id(4 ou 6B)][sw_ver(1B)]
    /// </summary>
    public static byte[] ConstruirFrameAutenticacao(string senha)
    {
        // 0x02 = software de monitoramento
        var swType = new byte[] { 0x02 };
        
        // A senha deve ter 4 ou 6 digitos.
        int tamSenha = senha.Length == 6 ? 6 : 4;
        var encSenha = ContactIdEncode(senha, tamSenha);
        
        // 0x10 = versao do software 1.0
        var swVer = new byte[] { 0x10 };

        var payload = new byte[1 + encSenha.Length + 1];
        swType.CopyTo(payload, 0);
        encSenha.CopyTo(payload, 1);
        swVer.CopyTo(payload, 1 + encSenha.Length);

        return ConstruirFrame(0xf0f0, payload);
    }

    // ----------------------------------------------------------------
    // Comandos PGM
    //
    // TODO: nenhum opcode de PGM encontrado nas fontes publicas do protocolo ISECNet.
    //       Requer documentacao oficial da Intelbras (solicitar via suporte).
    // ----------------------------------------------------------------
}
