using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

public class History
{
    public int Id { get; set; }

    public DateTime Data { get; set; }

    [Required]
    public int CentralId { get; set; }

    [ForeignKey(nameof(CentralId))]
    public Central? Central { get; set; }

    public int PGM { get; set; }

    [Required]
    [MaxLength(50)]
    public string Comando { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Resultado { get; set; } = string.Empty;
}
