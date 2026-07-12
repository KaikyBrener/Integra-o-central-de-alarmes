using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

public class CentralService
{
    private readonly AppDbContext _context;
    private readonly ConnectionService _connectionService;
    private readonly ILogger<CentralService> _logger;

    public CentralService(AppDbContext context, ConnectionService connectionService, ILogger<CentralService> logger)
    {
        _context = context;
        _connectionService = connectionService;
        _logger = logger;
    }

    private static CentralDto ParaDto(Central c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        IP = c.IP,
        Porta = c.Porta,
        Usuario = c.Usuario,
        BuildingId = c.BuildingId,
        BuildingNome = c.Building?.Nome,
        Fabricante = c.Fabricante,
        Modelo = c.Modelo,
        Firmware = c.Firmware,
        Status = c.Status,
        Latencia = c.Latencia
        // Senha propositalmente omitida: nunca é retornada pela API.
    };

    public async Task<IEnumerable<CentralDto>> ListarAsync()
    {
        var centrals = await _context.Centrals.Include(c => c.Building).AsNoTracking().ToListAsync();
        return centrals.Select(ParaDto);
    }

    public async Task<CentralDto?> ObterPorIdAsync(int id)
    {
        var central = await _context.Centrals.Include(c => c.Building).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return central == null ? null : ParaDto(central);
    }

    private async Task ValidarBuildingAsync(int buildingId)
    {
        var existe = await _context.Buildings.AnyAsync(b => b.Id == buildingId);
        if (!existe)
        {
            throw new BusinessException($"Prédio {buildingId} não encontrado.");
        }
    }

    public async Task<CentralDto> CriarAsync(CreateCentralDto dto)
    {
        await ValidarBuildingAsync(dto.BuildingId);

        var central = new Central
        {
            Nome = dto.Nome,
            IP = dto.IP,
            Porta = dto.Porta,
            Usuario = dto.Usuario,
            Senha = dto.Senha,
            BuildingId = dto.BuildingId,
            Fabricante = dto.Fabricante,
            Modelo = dto.Modelo,
            Firmware = dto.Firmware,
            Status = dto.Status,
            Latencia = dto.Latencia
        };

        _context.Centrals.Add(central);
        await _context.SaveChangesAsync();

        // Nunca logar a senha: apenas identificadores e dados não sensíveis.
        _logger.LogInformation("Central {CentralId} ({Nome}) criada para o Prédio {BuildingId}", central.Id, central.Nome, central.BuildingId);

        return ParaDto(central);
    }

    public async Task<bool> AtualizarAsync(int id, UpdateCentralDto dto)
    {
        var existente = await _context.Centrals.FindAsync(id);
        if (existente == null)
        {
            return false;
        }

        await ValidarBuildingAsync(dto.BuildingId);

        existente.Nome = dto.Nome;
        existente.IP = dto.IP;
        existente.Porta = dto.Porta;
        existente.Usuario = dto.Usuario;
        existente.Senha = dto.Senha;
        existente.BuildingId = dto.BuildingId;
        existente.Fabricante = dto.Fabricante;
        existente.Modelo = dto.Modelo;
        existente.Firmware = dto.Firmware;
        existente.Status = dto.Status;
        existente.Latencia = dto.Latencia;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Central {CentralId} atualizada", id);
        return true;
    }

    public async Task<bool> ExcluirAsync(int id)
    {
        var central = await _context.Centrals.FindAsync(id);
        if (central == null)
        {
            return false;
        }

        _context.Centrals.Remove(central);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Central {CentralId} excluída", id);
        return true;
    }

    public async Task<ConexaoResultDto> TestarConexaoAsync(TestarConexaoDto dto)
    {
        var resultado = await _connectionService.TestarConexao(dto.IP, dto.Porta, dto.Usuario, dto.Senha);

        return new ConexaoResultDto
        {
            Sucesso = resultado.Sucesso,
            Fabricante = resultado.Fabricante,
            Modelo = resultado.Modelo,
            Firmware = resultado.Firmware,
            Status = resultado.Status,
            LatenciaMs = resultado.LatenciaMs,
            Erro = resultado.Erro
        };
    }
}
