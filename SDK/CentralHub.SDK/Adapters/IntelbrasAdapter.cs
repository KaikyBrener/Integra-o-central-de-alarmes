namespace CentralHub.SDK.Adapters;

public class IntelbrasAdapter : TcpAdapterBase, IZonaAdapter
{
    /// <summary>Porta padrao do Receptor IP Intelbras (AMT), usada como heuristica de deteccao.</summary>
    public const int PortaPadrao = 9009;

    /// <inheritdoc/>
    /// <remarks>
    /// Fluxo real (ISECNet V2): abre uma nova conexao TCP dedicada, autentica
    /// (opcode 0xf0f0, fonte: goalarmeitbl/protocol.go) e consulta status
    /// (opcode 0x0b4a, fonte: comandos.go elvis-epx) para obter modelo/firmware.
    ///
    /// TODO (nao confirmado nas fontes publicas, validar com hardware real):
    ///   - Se a central exige autenticacao antes do OpcodeStatus quando a
    ///     conexao e DIRETA (sem Receptor IP) — aqui assumimos que sim.
    ///   - Se a resposta de autenticacao precisa ter o opcode validado contra
    ///     OpcodeRespostaSucesso (0xf0fe) para confirmar sucesso; hoje so
    ///     verificamos que voltou um frame com checksum valido.
    ///   - Reaproveitar a conexao ja testada em TcpAdapterBase.VerificarConectividade
    ///     em vez de abrir uma segunda conexao aqui (otimizacao futura, fora do
    ///     escopo desta integracao inicial).
    /// </remarks>
    protected override async Task<ConexaoResult> IdentificarAsync(
        string ip, int porta, string usuario, string senha, long latenciaMs)
    {
        using var client = await TcpConnectionHelper.AbrirConexaoAsync(ip, porta);
        if (client == null)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = "Falha ao abrir conexao dedicada para identificacao."
            };
        }

        var stream = client.GetStream();

        var authFrame = IsecNetProtocol.ConstruirFrameAutenticacao(senha);
        await IsecNetFrameIO.EnviarFrameAsync(stream, authFrame, CancellationToken.None);

        var authResposta = await IsecNetFrameIO.LerFrameAsync(stream);
        if (authResposta == null)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = "Sem resposta valida de autenticacao (frame invalido ou timeout)."
            };
        }

        var statusFrame = IsecNetProtocol.ConstruirFrame(IsecNetProtocol.OpcodeStatus);
        await IsecNetFrameIO.EnviarFrameAsync(stream, statusFrame, CancellationToken.None);

        var statusResposta = await IsecNetFrameIO.LerFrameAsync(stream);
        if (statusResposta == null)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = "Sem resposta valida ao OpcodeStatus (frame invalido ou timeout)."
            };
        }

        // DATA = frame sem o byte de LEN (indice 0) e sem o byte de CS (ultimo).
        var payload = statusResposta[1..^1];
        var (modelo, firmware) = IsecNetProtocol.ParsearRespostaStatus(payload);

        return new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "Intelbras",
            Modelo = modelo,
            Firmware = firmware,
            Status = "Online",
            LatenciaMs = latenciaMs
        };
    }

    // AcionarPGM / DesligarPGM / PulsoPGM: herdados de TcpAdapterBase.
    // TODO: nenhum opcode de PGM encontrado nas fontes publicas do ISECNet.
    // Retornam "Protocolo nao implementado." ate obter documentacao oficial
    // da Intelbras (solicitar via suporte, conforme ja registrado no MVP anterior).

    /// <inheritdoc/>
    /// <remarks>
    /// Usa OpcodeBypassZona (0x401f, fonte: elvis-epx/alarme-intelbras).
    /// Payload confirmado: [zona-1, 0x01=bypass].
    /// </remarks>
    public Task<ComandoResult> BypassZona(string ip, int porta, string usuario, string senha, int zona)
        => EnviarComandoZonaAsync(ip, porta, senha, zona, ativarBypass: true);

    /// <inheritdoc/>
    /// <remarks>
    /// Usa OpcodeBypassZona (0x401f). Payload confirmado: [zona-1, 0x00=cancelar].
    /// </remarks>
    public Task<ComandoResult> CancelarBypassZona(string ip, int porta, string usuario, string senha, int zona)
        => EnviarComandoZonaAsync(ip, porta, senha, zona, ativarBypass: false);

    private static async Task<ComandoResult> EnviarComandoZonaAsync(
        string ip, int porta, string senha, int zona, bool ativarBypass)
    {
        using var client = await TcpConnectionHelper.AbrirConexaoAsync(ip, porta);
        if (client == null)
        {
            return new ComandoResult { Sucesso = false, Resultado = "Falha ao conectar para enviar comando de zona." };
        }

        var stream = client.GetStream();

        var authFrame = IsecNetProtocol.ConstruirFrameAutenticacao(senha);
        await IsecNetFrameIO.EnviarFrameAsync(stream, authFrame, CancellationToken.None);
        if (await IsecNetFrameIO.LerFrameAsync(stream) == null)
        {
            return new ComandoResult { Sucesso = false, Resultado = "Falha na autenticacao." };
        }

        var payload = new byte[] { (byte)(zona - 1), (byte)(ativarBypass ? 0x01 : 0x00) };
        var comandoFrame = IsecNetProtocol.ConstruirFrame(IsecNetProtocol.OpcodeBypassZona, payload);
        await IsecNetFrameIO.EnviarFrameAsync(stream, comandoFrame, CancellationToken.None);

        var resposta = await IsecNetFrameIO.LerFrameAsync(stream);
        if (resposta == null)
        {
            return new ComandoResult { Sucesso = false, Resultado = "Sem resposta ao comando de bypass (frame invalido ou timeout)." };
        }

        // TODO: assim como na autenticacao, nao validamos o opcode de resposta
        // contra OpcodeRespostaSucesso (0xf0fe) — so confirmamos que um frame
        // com checksum valido voltou. Ajustar quando soubermos o formato exato
        // da resposta de sucesso/erro para este comando especifico.
        return new ComandoResult
        {
            Sucesso = true,
            Resultado = ativarBypass ? $"Zona {zona} bypass ativado" : $"Zona {zona} bypass cancelado"
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// TODO: nenhum opcode de consulta de status de zonas (aberta/fechada/
    /// disparo) encontrado nas fontes publicas do ISECNet analisadas
    /// (elvis-epx/alarme-intelbras, goalarmeitbl/protocol.go). Requer
    /// documentacao oficial da Intelbras ou captura de trafego real com uma
    /// central conectada para mapear o opcode e o formato da resposta.
    /// </remarks>
    public Task<IEnumerable<ZonaResult>> ConsultarZonas(string ip, int porta, string usuario, string senha)
        => throw new NotImplementedException(
            "ConsultarZonas: opcode nao confirmado para ISECNet. Ver TODO em IsecNetProtocol.cs / IntelbrasAdapter.cs.");
}
