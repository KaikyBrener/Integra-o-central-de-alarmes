using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Controllers;

/// <summary>Consulta os eventos recebidos das centrais (alarme, arme/desarme, problemas, PGM, bypass...).</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventController : ControllerBase
{
    private const int LimiteMaximo = 1000;

    private readonly AppDbContext _context;

    public EventController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>Lista os eventos mais recentes, opcionalmente filtrados por Central ou número de série.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventDto>>> Listar(
        [FromQuery] int? centralId, [FromQuery] string? serial, [FromQuery] int limite = 100)
    {
        limite = Math.Clamp(limite, 1, LimiteMaximo);

        var query = _context.Eventos.AsNoTracking().Include(e => e.Central).AsQueryable();
        if (centralId.HasValue) query = query.Where(e => e.CentralId == centralId.Value);
        if (!string.IsNullOrWhiteSpace(serial)) query = query.Where(e => e.Serial == serial);

        // SQLite não ordena DateTime no servidor de forma confiável com todos os provedores; Id cresce junto.
        var eventos = await query.OrderByDescending(e => e.Id).Take(limite).ToListAsync();

        return Ok(eventos.Select(e => new EventDto
        {
            Id = e.Id,
            DataHora = e.DataHora,
            CentralId = e.CentralId,
            CentralNome = e.Central?.Nome,
            Fabricante = e.Fabricante,
            Serial = e.Serial,
            Conta = e.Conta,
            Codigo = e.Codigo,
            Tipo = e.Tipo,
            Restauracao = e.Restauracao,
            Particao = e.Particao,
            Zona = e.Zona,
            Usuario = e.Usuario,
            Pgm = e.Pgm,
            Descricao = e.Descricao
        }));
    }
}
