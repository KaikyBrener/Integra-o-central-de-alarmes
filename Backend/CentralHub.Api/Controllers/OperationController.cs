using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>Envia comandos de PGM para as Centrais e consulta o histórico de operações.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OperationController : ControllerBase
{
    private readonly OperationService _service;
    private readonly ILogger<OperationController> _logger;

    public OperationController(OperationService service, ILogger<OperationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Envia um comando (Pulso, Ligar ou Desligar) para um PGM de uma Central.</summary>
    [HttpPost("enviar")]
    [ProducesResponseType(typeof(HistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryDto>> Enviar(ComandoDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Recebida requisição de comando {Comando} para Central {CentralId}", dto.Comando, dto.CentralId);

        var history = await _service.EnviarComandoAsync(dto);
        return Ok(history);
    }

    /// <summary>Arma ou desarma uma partição (centrais que suportam: hoje JFL).</summary>
    [HttpPost("particao")]
    [ProducesResponseType(typeof(HistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryDto>> Particao(ParticaoComandoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AlterarParticaoAsync(dto));
    }

    /// <summary>Anula (bypass) ou reativa uma zona (centrais que suportam: hoje JFL).</summary>
    [HttpPost("bypass")]
    [ProducesResponseType(typeof(HistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryDto>> Bypass(BypassComandoDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AlterarBypassAsync(dto));
    }

    /// <summary>Consulta o estado das partições (lido da própria central).</summary>
    [HttpGet("particoes/{centralId}")]
    [ProducesResponseType(typeof(IEnumerable<ParticaoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ParticaoDto>>> Particoes(int centralId)
        => Ok(await _service.ConsultarParticoesAsync(centralId));

    /// <summary>Consulta o estado das zonas (lido da própria central).</summary>
    [HttpGet("zonas/{centralId}")]
    [ProducesResponseType(typeof(IEnumerable<ZonaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ZonaDto>>> Zonas(int centralId)
        => Ok(await _service.ConsultarZonasAsync(centralId));

    /// <summary>Lista o histórico de comandos enviados, opcionalmente filtrado por Central.</summary>
    [HttpGet("historico")]
    [ProducesResponseType(typeof(IEnumerable<HistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HistoryDto>>> Historico([FromQuery] int? centralId)
    {
        return Ok(await _service.ObterHistoricoAsync(centralId));
    }
}
