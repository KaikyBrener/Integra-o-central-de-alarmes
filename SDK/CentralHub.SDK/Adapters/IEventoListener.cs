namespace CentralHub.SDK.Adapters;

/// <summary>
/// Categoria de evento emitido pela central.
/// TODO: cada fabricante tem seu próprio conjunto de códigos de evento
/// (ex.: Contact ID na Intelbras); o mapeamento código → TipoEvento ainda
/// não foi feito para nenhum fabricante.
/// </summary>
public enum TipoEvento
{
    Alarme,
    Arme,
    Desarme,
    Energia,
    Bateria,
    Comunicacao,
    // Acrescentados com o suporte a eventos JFL (valores novos ao final para nao quebrar os existentes).
    Problema,
    Pgm,
    Bypass,
    Programacao,
    Teste,
    Geral
}

/// <summary>Um evento emitido pela central em um dado momento.</summary>
public class EventoCentral
{
    public DateTime DataHora { get; set; } = DateTime.Now;
    public TipoEvento Tipo { get; set; }
    public int? Particao { get; set; }
    public int? Zona { get; set; }
    public string? Detalhe { get; set; }

    // --- Campos acrescentados com o suporte a eventos reais (todos opcionais) ---

    /// <summary>Fabricante que originou o evento (ex.: "JFL").</summary>
    public string? Fabricante { get; set; }

    /// <summary>Identificador estavel da central no protocolo (numero de serie na JFL).</summary>
    public string? Serial { get; set; }

    /// <summary>Conta do monitoramento programada na central.</summary>
    public string? Conta { get; set; }

    /// <summary>Codigo Contact ID com qualificador, como a central o enviou (ex.: "1130").</summary>
    public string? Codigo { get; set; }

    /// <summary>True quando o evento e uma restauracao.</summary>
    public bool Restauracao { get; set; }

    public int? Usuario { get; set; }
    public int? Pgm { get; set; }

    /// <summary>Bytes do quadro original em hexadecimal, para auditoria e depuracao de protocolo.</summary>
    public string? Bruto { get; set; }
}

/// <summary>
/// Suporte opcional a eventos assíncronos da central (alarme, arme/desarme,
/// energia, bateria, comunicação).
///
/// ATENÇÃO ARQUITETURAL — motivo de isto NÃO estar em ICentralAdapter:
/// PGM e Zonas são "pull" (o sistema pergunta, a central responde, na mesma
/// conexão que <see cref="TcpAdapterBase"/> já abre como cliente). Eventos
/// são "push": a central é quem inicia a comunicação quando algo acontece.
/// Isso já está documentado em JflActiveProtocol.cs para a linha JFL Active
/// (a central conecta como cliente TCP; o sistema precisaria ser o servidor)
/// e se aplica também à Intelbras via Receptor IP.
///
/// Ou seja: implementar isto de verdade exige um componente novo (um
/// TcpListener/host rodando junto da API), não apenas um método a mais em
/// um adapter que conecta como cliente. Por isso o contrato fica isolado
/// aqui, sem nenhuma classe implementando-o ainda.
///
/// TODO antes de implementar:
///   1. Serviço de host (TcpListener) para aceitar conexões iniciadas
///      pelas centrais.
///   2. Parsing dos frames de evento por fabricante — checksums já mapeados
///      (IsecNetProtocol / JflActiveProtocol), mas os opcodes/bytes de
///      evento (alarme, arme/desarme, energia, bateria, comunicação) ainda
///      não estão confirmados nas fontes públicas para nenhum fabricante.
///   3. Persistência (tabela de eventos, análoga a History) — desenhar o
///      schema só depois de saber o que cada protocolo realmente devolve.
/// </summary>
public interface IEventoListener
{
    event Action<EventoCentral>? EventoRecebido;

    Task IniciarEscutaAsync(CancellationToken cancellationToken);
}
