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

    /// <summary>Lista o histórico de comandos enviados, opcionalmente filtrado por Central.</summary>
    [HttpGet("historico")]
    [ProducesResponseType(typeof(IEnumerable<HistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HistoryDto>>> Historico([FromQuery] int? centralId)
    {
        return Ok(await _service.ObterHistoricoAsync(centralId));
    }
}
