using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using CentralHub.SDK.Adapters;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

public class OperationService
{
    private static readonly string[] ComandosValidos = { "Pulso", "Ligar", "Desligar" };

    private readonly AppDbContext _context;
    private readonly ILogger<OperationService> _logger;

    public OperationService(AppDbContext context, ILogger<OperationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static HistoryDto ParaDto(History h) => new()
    {
        Id = h.Id,
        Data = h.Data,
        CentralId = h.CentralId,
        CentralNome = h.Central?.Nome,
        PGM = h.PGM,
        Comando = h.Comando,
        Resultado = h.Resultado
    };

    public async Task<HistoryDto> EnviarComandoAsync(ComandoDto dto)
    {
        if (!ComandosValidos.Contains(dto.Comando, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException($"Comando inválido. Valores aceitos: {string.Join(", ", ComandosValidos)}.");
        }

        if (dto.Comando.Equals("Pulso", StringComparison.OrdinalIgnoreCase) && dto.TempoPulsoMs <= 0)
        {
            throw new BusinessException("Tempo do Pulso deve ser maior que zero.");
        }

        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == dto.CentralId);
        if (central == null)
        {
            throw new BusinessException($"Central {dto.CentralId} não encontrada.", statusCode: 404);
        }

        var adapter = AdapterFactory.Criar(AdapterFactory.ResolverPorNome(central.Fabricante));

        // Nunca logar a senha da central: apenas identificadores e o comando executado.
        _logger.LogInformation(
            "Enviando comando {Comando} para Central {CentralId} ({Fabricante}), PGM {PGM}",
            dto.Comando, central.Id, central.Fabricante, dto.PGM);

        ComandoResult resultado = dto.Comando.ToLower() switch
        {
            "ligar" => await adapter.AcionarPGM(central.IP, central.Porta, central.Usuario, central.Senha, dto.PGM),
            "desligar" => await adapter.DesligarPGM(central.IP, central.Porta, central.Usuario, central.Senha, dto.PGM),
            "pulso" => await adapter.PulsoPGM(central.IP, central.Porta, central.Usuario, central.Senha, dto.PGM, dto.TempoPulsoMs),
            _ => new ComandoResult { Sucesso = false, Resultado = "Comando inválido" }
        };

        var history = new History
        {
            Data = DateTime.Now,
            CentralId = dto.CentralId,
            PGM = dto.PGM,
            Comando = dto.Comando,
            Resultado = resultado.Resultado
        };

        _context.Histories.Add(history);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Comando registrado no histórico {HistoryId}: {Resultado}", history.Id, history.Resultado);

        return ParaDto(history);
    }

    public async Task<IEnumerable<HistoryDto>> ObterHistoricoAsync(int? centralId)
    {
        var query = _context.Histories.Include(h => h.Central).OrderByDescending(h => h.Data).AsQueryable();

        if (centralId.HasValue)
        {
            query = query.Where(h => h.CentralId == centralId.Value);
        }

        var historico = await query.ToListAsync();
        return historico.Select(ParaDto);
    }
}
