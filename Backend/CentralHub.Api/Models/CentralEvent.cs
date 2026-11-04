using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

/// <summary>
/// Evento recebido de uma central (push). CentralId é nulo quando o evento vem de uma central ainda
/// não cadastrada (identificada apenas pelo Serial do protocolo).
/// </summary>
public class CentralEvent
{
    public int Id { get; set; }

    public DateTime DataHora { get; set; }

    public int? CentralId { get; set; }

    [ForeignKey(nameof(CentralId))]
    public Central? Central { get; set; }

    [MaxLength(50)]
    public string? Fabricante { get; set; }

    [MaxLength(50)]
    public string? Serial { get; set; }

    [MaxLength(20)]
    public string? Conta { get; set; }

    /// <summary>Código Contact ID com qualificador, como a central enviou (ex.: 1130).</summary>
    [MaxLength(10)]
    public string? Codigo { get; set; }

    [Required]
    [MaxLength(30)]
    public string Tipo { get; set; } = string.Empty;

    public bool Restauracao { get; set; }

    public int? Particao { get; set; }
    public int? Zona { get; set; }
    public int? Usuario { get; set; }
    public int? Pgm { get; set; }

    [MaxLength(500)]
    public string? Descricao { get; set; }

    /// <summary>Quadro original em hexadecimal (auditoria de protocolo).</summary>
    [MaxLength(600)]
    public string? Bruto { get; set; }
}
