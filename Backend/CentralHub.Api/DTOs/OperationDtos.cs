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
    public bool Sucesso { get; set; }
    public string Resultado { get; set; } = string.Empty;
}

/// <summary>Armar ou desarmar uma partição.</summary>
public class ParticaoComandoDto
{
    [Required]
    public int CentralId { get; set; }

    [Required]
    public int Particao { get; set; }

    /// <summary>Valores aceitos: Armar, Desarmar.</summary>
    [Required]
    public string Acao { get; set; } = string.Empty;
}

/// <summary>Anular (bypass) ou reativar uma zona.</summary>
public class BypassComandoDto
{
    [Required]
    public int CentralId { get; set; }

    [Required]
    public int Zona { get; set; }

    /// <summary>Valores aceitos: Anular, Reativar.</summary>
    [Required]
    public string Acao { get; set; } = string.Empty;
}

public class ParticaoDto
{
    public int Numero { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool EmAlarme { get; set; }
}

public class ZonaDto
{
    public int Numero { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Descricao { get; set; }
}
