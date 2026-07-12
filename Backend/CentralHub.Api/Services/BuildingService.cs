using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Exceção de regra de negócio, tratada pelos Controllers como erro do cliente (400/404).
/// </summary>
public class BusinessException : Exception
{
    public int StatusCode { get; }

    public BusinessException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class BuildingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BuildingService> _logger;

    public BuildingService(AppDbContext context, ILogger<BuildingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static BuildingDto ParaDto(Building b) => new()
    {
        Id = b.Id,
        Nome = b.Nome,
        Descricao = b.Descricao
    };

    public async Task<IEnumerable<BuildingDto>> ListarAsync()
    {
        var buildings = await _context.Buildings.AsNoTracking().ToListAsync();
        return buildings.Select(ParaDto);
    }

    public async Task<BuildingDto?> ObterPorIdAsync(int id)
    {
        var building = await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        return building == null ? null : ParaDto(building);
    }

    public async Task<BuildingDto> CriarAsync(CreateBuildingDto dto)
    {
        var building = new Building { Nome = dto.Nome, Descricao = dto.Descricao };

        _context.Buildings.Add(building);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Prédio {BuildingId} ({Nome}) criado", building.Id, building.Nome);
        return ParaDto(building);
    }

    public async Task<bool> AtualizarAsync(int id, UpdateBuildingDto dto)
    {
        var existente = await _context.Buildings.FindAsync(id);
        if (existente == null)
        {
            return false;
        }

        existente.Nome = dto.Nome;
        existente.Descricao = dto.Descricao;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Prédio {BuildingId} atualizado", id);
        return true;
    }

    public async Task<bool?> ExcluirAsync(int id)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return null;
        }

        var possuiCentrais = await _context.Centrals.AnyAsync(c => c.BuildingId == id);
        if (possuiCentrais)
        {
            _logger.LogWarning("Tentativa de excluir Prédio {BuildingId} que possui centrais vinculadas", id);
            throw new BusinessException("Não é possível excluir um prédio que possui centrais cadastradas.");
        }

        _context.Buildings.Remove(building);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Prédio {BuildingId} excluído", id);
        return true;
    }
}
