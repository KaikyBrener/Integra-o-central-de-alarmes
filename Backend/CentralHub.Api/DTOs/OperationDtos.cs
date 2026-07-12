using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para envio de um comando a uma Central/PGM.</summary>
public class ComandoDto
{
    [Required]
    public int CentralId { get; set; }

    [Required]
    public int PGM { get; set; }

    /// <summary>Valores aceitos: Pulso, Ligar, Desligar.</summary>
    [Required]
    public string Comando { get; set; } = string.Empty;

    /// <summary>Obrigatório apenas quando Comando = "Pulso".</summary>
    public int TempoPulsoMs { get; set; }
}

/// <summary>Dados de saída de um registro de histórico.</summary>
public class HistoryDto
{
    public int Id { get; set; }
    public DateTime Data { get; set; }
    public int CentralId { get; set; }
    public string? CentralNome { get; set; }
    public int PGM { get; set; }
    public string Comando { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
}
