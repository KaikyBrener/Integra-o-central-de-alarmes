using System.ComponentModel.DataAnnotations;

namespace CentralHub.Api.DTOs;

/// <summary>Dados de entrada para criação de uma Central.</summary>
public class CreateCentralDto
{
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

/// <summary>Dados de entrada para atualização de uma Central.</summary>
public class UpdateCentralDto : CreateCentralDto
{
}

/// <summary>
/// Dados de saída de uma Central. Nunca inclui a Senha,
/// para evitar exposição de credenciais pela API.
/// </summary>
public class CentralDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string IP { get; set; } = string.Empty;
    public int Porta { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public int BuildingId { get; set; }
    public string? BuildingNome { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? Firmware { get; set; }
    public string? Status { get; set; }
    public long? Latencia { get; set; }
}

/// <summary>Dados de entrada para o teste de conexão com uma Central.</summary>
public class TestarConexaoDto
{
    [Required]
    [MaxLength(45)]
    public string IP { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Porta { get; set; }

    [Required]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    public string Senha { get; set; } = string.Empty;
}

/// <summary>Resultado do teste de conexão com uma Central.</summary>
public class ConexaoResultDto
{
    public bool Sucesso { get; set; }
    public string? Fabricante { get; set; }
    public string? Modelo { get; set; }
    public string? Firmware { get; set; }
    public string? Status { get; set; }
    public long LatenciaMs { get; set; }
    public string? Erro { get; set; }
}
