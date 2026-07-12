using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>Gerencia o cadastro de Centrais de alarme e o teste de conectividade.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CentralController : ControllerBase
{
    private readonly CentralService _service;
    private readonly ILogger<CentralController> _logger;

    public CentralController(CentralService service, ILogger<CentralController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista todas as centrais cadastradas (sem retornar a senha).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CentralDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CentralDto>>> GetAll()
    {
        return Ok(await _service.ListarAsync());
    }

    /// <summary>Obtém uma central pelo Id (sem retornar a senha).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CentralDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CentralDto>> GetById(int id)
    {
        var central = await _service.ObterPorIdAsync(id);
        if (central == null)
        {
            _logger.LogWarning("Central {CentralId} não encontrada", id);
            return NotFound($"Central {id} não encontrada.");
        }
        return Ok(central);
    }

    /// <summary>Cadastra uma nova central, vinculada a um Prédio existente.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CentralDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CentralDto>> Create(CreateCentralDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza uma central existente.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateCentralDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var atualizado = await _service.AtualizarAsync(id, dto);
        if (!atualizado)
        {
            return NotFound($"Central {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>Remove uma central.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var excluido = await _service.ExcluirAsync(id);
        if (!excluido)
        {
            return NotFound($"Central {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>
    /// Testa a conectividade com uma central antes de salvá-la, identificando
    /// Fabricante, Modelo, Firmware, Status e Latência.
    /// </summary>
    [HttpPost("testar-conexao")]
    [ProducesResponseType(typeof(ConexaoResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConexaoResultDto>> TestarConexao(TestarConexaoDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _service.TestarConexaoAsync(dto);
        return Ok(resultado);
    }
}
