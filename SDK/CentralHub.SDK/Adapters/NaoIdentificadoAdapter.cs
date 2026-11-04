namespace CentralHub.SDK.Adapters;

/// <summary>
/// Adapter usado quando o fabricante da Central ainda não foi identificado. Falha de forma explícita
/// em toda operação para que um comando nunca seja registrado como sucesso sem ter chegado a uma
/// central real (ao contrário do FakeAdapter, que só deve ser usado quando solicitado).
/// </summary>
public class NaoIdentificadoAdapter : ICentralAdapter
{
    private const string Mensagem =
        "Fabricante da central não identificado. Use 'Testar Conexão' para identificar a central antes de operar.";

    public Task<bool> Conectar(string ip, int porta, string usuario, string senha) => Task.FromResult(false);

    public Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
        => Task.FromResult(new ConexaoResult { Sucesso = false, Status = "Offline", Erro = Mensagem });

    public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = Mensagem });

    public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = Mensagem });

    public Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
        => Task.FromResult(new ComandoResult { Sucesso = false, Resultado = Mensagem });
}
