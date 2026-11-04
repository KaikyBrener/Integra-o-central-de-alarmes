namespace CentralHub.Api.DTOs;

/// <summary>Evento recebido de uma central (push), já interpretado.</summary>
public class EventDto
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public int? CentralId { get; set; }
    public string? CentralNome { get; set; }
    public string? Fabricante { get; set; }
    public string? Serial { get; set; }
    public string? Conta { get; set; }
    public string? Codigo { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public bool Restauracao { get; set; }
    public int? Particao { get; set; }
    public int? Zona { get; set; }
    public int? Usuario { get; set; }
    public int? Pgm { get; set; }
    public string? Descricao { get; set; }
}
