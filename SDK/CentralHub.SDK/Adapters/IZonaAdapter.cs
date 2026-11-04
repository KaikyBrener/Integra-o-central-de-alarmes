namespace CentralHub.SDK.Adapters;

/// <summary>
/// Suporte opcional a consulta e bypass de zonas.
///
/// Propositalmente FORA de <see cref="ICentralAdapter"/>: assim nenhum
/// adapter existente (FakeAdapter, IntelbrasAdapter, JflAdapter) precisa
/// mudar hoje. Um adapter concreto só implementa esta interface quando o
/// protocolo do fabricante suportar a operação de fato.
///
/// Mesmo padrão de assinatura usado em PGM (ip, porta, usuario, senha, ...)
/// para manter consistência com <see cref="ICentralAdapter"/>.
///
/// TODO: nenhuma implementação real ainda.
///   - Intelbras: opcode de bypass já mapeado
///     (<see cref="IsecNetProtocol.OpcodeBypassZona"/> = 0x401f), mas falta
///     confirmar opcode/estrutura de resposta de ConsultarZonas.
///   - JFL: nenhum opcode de zona nas fontes públicas (fernac03/JFL_ACTIVE).
/// </summary>
public interface IZonaAdapter
{
    Task<IEnumerable<ZonaResult>> ConsultarZonas(string ip, int porta, string usuario, string senha);

    Task<ComandoResult> BypassZona(string ip, int porta, string usuario, string senha, int zona);

    Task<ComandoResult> CancelarBypassZona(string ip, int porta, string usuario, string senha, int zona);
}
