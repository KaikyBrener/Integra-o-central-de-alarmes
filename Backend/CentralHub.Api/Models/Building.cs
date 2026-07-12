using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.Models;

public class Building
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }
}
