namespace CentralHub.SDK.Adapters;

/// <summary>
/// Classe base para adapters que se comunicam via TCP/IP.
/// Centraliza o transporte (conectividade, latencia, timeout) e delega
/// a identificacao do fabricante/modelo/firmware para cada adapter concreto,
/// via o metodo abstrato <see cref="IdentificarAsync"/>.
///
/// Responsabilidade desta classe:
///   - Testar conectividade TCP (transporte).
///   - Retornar o resultado padrao de "Offline" quando TCP falha.
///   - Chamar IdentificarAsync somente quando TCP esta aberto.
///
/// Responsabilidade do adapter concreto:
///   - Implementar IdentificarAsync com o protocolo do fabricante.
///   - Sobrescrever AcionarPGM/DesligarPGM/PulsoPGM quando o protocolo estiver disponivel.
/// </summary>
public abstract class TcpAdapterBase : ICentralAdapter
{
    /// <inheritdoc/>
    public async Task<bool> Conectar(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, _, _) = await TcpConnectionHelper.TestarConexao(ip, porta);
        return sucesso;
    }

    /// <inheritdoc/>
    public async Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
    {
        var (sucesso, latenciaMs, erro) = await TcpConnectionHelper.TestarConexao(ip, porta);

        if (!sucesso)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Status = "Offline",
                LatenciaMs = latenciaMs,
                Erro = erro
            };
        }

        return await IdentificarAsync(ip, porta, usuario, senha, latenciaMs);
    }

    /// <summary>
    /// Executado apos conexao TCP bem-sucedida. Cada adapter implementa
    /// o protocolo do fabricante aqui para identificar modelo e firmware.
    /// Enquanto o protocolo nao estiver disponivel, retorna valores estaticos
    /// com Modelo = "Desconhecido" e Firmware = "Desconhecido".
    /// </summary>
    protected abstract Task<ConexaoResult> IdentificarAsync(
        string ip, int porta, string usuario, string senha, long latenciaMs);

    /// <inheritdoc/>
    /// <remarks>
    /// Padrao: retorna falha explicita. Sobrescreva quando o protocolo
    /// do fabricante estiver implementado.
    /// </remarks>
    public virtual Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = "Protocolo nao implementado." });

    /// <inheritdoc/>
    public virtual Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = "Protocolo nao implementado." });

    /// <inheritdoc/>
    public virtual Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = "Protocolo nao implementado." });
}
