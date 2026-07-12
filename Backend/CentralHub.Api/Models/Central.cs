using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CentralHub.Api.Models;

public class Central
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [MaxLength(45)]
    public string IP { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Porta { get; set; }

    [Required]
    [MaxLength(100)]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Senha { get; set; } = string.Empty;

    [Required]
    public int BuildingId { get; set; }

    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }

    [MaxLength(100)]
    public string? Fabricante { get; set; }

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? Firmware { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public long? Latencia { get; set; }
}
