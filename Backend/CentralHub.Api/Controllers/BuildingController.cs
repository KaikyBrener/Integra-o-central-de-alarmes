using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>Gerencia o cadastro de Prédios.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BuildingController : ControllerBase
{
    private readonly BuildingService _service;
    private readonly ILogger<BuildingController> _logger;

    public BuildingController(BuildingService service, ILogger<BuildingController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista todos os prédios cadastrados.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BuildingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BuildingDto>>> GetAll()
    {
        return Ok(await _service.ListarAsync());
    }

    /// <summary>Obtém um prédio pelo Id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BuildingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BuildingDto>> GetById(int id)
    {
        var building = await _service.ObterPorIdAsync(id);
        if (building == null)
        {
            _logger.LogWarning("Prédio {BuildingId} não encontrado", id);
            return NotFound($"Prédio {id} não encontrado.");
        }
        return Ok(building);
    }

    /// <summary>Cadastra um novo prédio.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BuildingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BuildingDto>> Create(CreateBuildingDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza um prédio existente.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateBuildingDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var atualizado = await _service.AtualizarAsync(id, dto);
        if (!atualizado)
        {
            return NotFound($"Prédio {id} não encontrado.");
        }
        return NoContent();
    }

    /// <summary>Remove um prédio. Falha se houver centrais vinculadas.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var resultado = await _service.ExcluirAsync(id);
        if (resultado == null)
        {
            return NotFound($"Prédio {id} não encontrado.");
        }
        return NoContent();
    }
}
