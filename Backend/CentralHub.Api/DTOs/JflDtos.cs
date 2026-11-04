namespace CentralHub.Api.DTOs;

/// <summary>Configuração e estado do servidor de monitoramento JFL.</summary>
public class JflServidorDto
{
    public bool Habilitado { get; set; }
    public int? PortaEmUso { get; set; }
    public int PortaConfigurada { get; set; }
    public string BindAddress { get; set; } = string.Empty;
    public int KeepAliveMinutes { get; set; }
    public bool SomenteLeitura { get; set; }
    public bool AceitaCentraisDesconhecidas { get; set; }
    public bool LogQuadrosBrutos { get; set; }
    public int SessoesAtivas { get; set; }
    public int CapacidadeLog { get; set; }
}

/// <summary>Perfil de um modelo JFL (tetos do datasheet oficial e nível de validação).</summary>
public class JflModeloDto
{
    public string ByteModelo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Placa { get; set; } = string.Empty;
    public int Particoes { get; set; }
    public int Zonas { get; set; }
    public int Pgms { get; set; }
    public bool Eletrificador { get; set; }
    public bool ComandosHabilitados { get; set; }
    public bool ValidadoEmHardware { get; set; }
    public string Evidencia { get; set; } = string.Empty;
}

/// <summary>Central JFL atualmente conectada ao servidor.</summary>
public class JflSessaoDto
{
    public string Serial { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public DateTime ConectadaEm { get; set; }
    public DateTime UltimoQuadroEm { get; set; }
    public DateTime? UltimoStatusEm { get; set; }
    public string Firmware { get; set; } = string.Empty;
    public string Mac { get; set; } = string.Empty;
    public string Imei { get; set; } = string.Empty;
    public int Sinal { get; set; }
    public JflModeloDto Modelo { get; set; } = new();
    public int? CentralId { get; set; }
    public string? CentralNome { get; set; }
}

public class JflParticaoStatusDto
{
    public int Numero { get; set; }
    public bool Programada { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool EmAlarme { get; set; }
    public string Bruto { get; set; } = string.Empty;
}

public class JflZonaStatusDto
{
    public int Numero { get; set; }
    public string Estado { get; set; } = string.Empty;
    /// <summary>Atributo "permite inibir" informado pela própria central no status (P-INIB).</summary>
    public bool PermiteInibir { get; set; }
}

public class JflPgmStatusDto
{
    public int Numero { get; set; }
    public bool Ligada { get; set; }
    /// <summary>Nulo quando o quadro de status não traz o byte de permissão desta PGM.</summary>
    public bool? Permitida { get; set; }
}

public class JflProblemaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Grupo { get; set; } = string.Empty;
}

/// <summary>Status lido da central (quadro de status decodificado).</summary>
public class JflStatusDto
{
    public string Serial { get; set; } = string.Empty;
    public DateTime? LidoEm { get; set; }
    /// <summary>False quando a central não respondeu agora e o status exibido é o último recebido.</summary>
    public bool Atualizado { get; set; }
    public string? Aviso { get; set; }
    public string Relogio { get; set; } = string.Empty;
    public double BateriaVolts { get; set; }
    public string Eletrificador { get; set; } = string.Empty;
    public int TamanhoQuadro { get; set; }
    public List<JflParticaoStatusDto> Particoes { get; set; } = new();
    public List<JflZonaStatusDto> Zonas { get; set; } = new();
    public List<JflPgmStatusDto> Pgms { get; set; } = new();
    public List<JflProblemaDto> Problemas { get; set; } = new();
}

/// <summary>Entrada do log de protocolo JFL.</summary>
public class JflLogDto
{
    public long Id { get; set; }
    public DateTime DataHora { get; set; }
    public string? Serial { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string Direcao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Comando { get; set; }
    public string? Hex { get; set; }
    public string Descricao { get; set; } = string.Empty;
}
