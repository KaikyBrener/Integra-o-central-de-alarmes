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

        var history = await RegistrarAsync(dto.CentralId, dto.PGM, dto.Comando, resultado);

        _logger.LogInformation("Comando registrado no histórico {HistoryId}: {Resultado}", history.Id, history.Resultado);

        return ParaDto(history);
    }

    /// <summary>
    /// Grava o resultado no histórico. O ComandoResult.Sucesso não tem coluna própria no modelo atual
    /// (sem migrations), então uma falha é marcada com o prefixo "FALHA:" para nunca parecer sucesso.
    /// </summary>
    private async Task<History> RegistrarAsync(int centralId, int pgm, string comando, ComandoResult resultado)
    {
        var texto = resultado.Sucesso ? resultado.Resultado : $"FALHA: {resultado.Resultado}";
        if (texto.Length > 500) texto = texto[..500];

        var history = new History
        {
            Data = DateTime.Now,
            CentralId = centralId,
            PGM = pgm,
            Comando = comando.Length > 50 ? comando[..50] : comando,
            Resultado = texto
        };

        _context.Histories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }

    private async Task<(Central Central, ICentralAdapter Adapter)> ObterCentralEAdapterAsync(int centralId)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId);
        if (central == null)
        {
            throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);
        }

        return (central, AdapterFactory.Criar(AdapterFactory.ResolverPorNome(central.Fabricante)));
    }

    private static T ExigirRecurso<T>(ICentralAdapter adapter, Central central, string recurso) where T : class
    {
        return adapter as T ?? throw new BusinessException(
            $"A central '{central.Nome}' (fabricante: {central.Fabricante ?? "não identificado"}) não suporta {recurso} neste CentralHub.");
    }

    /// <summary>Arma ou desarma uma partição.</summary>
    public async Task<HistoryDto> AlterarParticaoAsync(ParticaoComandoDto dto)
    {
        var armar = dto.Acao.Equals("Armar", StringComparison.OrdinalIgnoreCase);
        if (!armar && !dto.Acao.Equals("Desarmar", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Ação inválida. Valores aceitos: Armar, Desarmar.");
        }

        var (central, adapter) = await ObterCentralEAdapterAsync(dto.CentralId);
        var particoes = ExigirRecurso<IParticaoAdapter>(adapter, central, "arme/desarme de partições");

        _logger.LogInformation("Enviando {Acao} partição {Particao} para Central {CentralId} ({Fabricante})",
            dto.Acao, dto.Particao, central.Id, central.Fabricante);

        var resultado = armar
            ? await particoes.ArmarParticao(central.IP, central.Porta, central.Usuario, central.Senha, dto.Particao)
            : await particoes.DesarmarParticao(central.IP, central.Porta, central.Usuario, central.Senha, dto.Particao);

        var history = await RegistrarAsync(central.Id, 0, $"{(armar ? "Armar" : "Desarmar")} partição {dto.Particao}", resultado);
        return ParaDto(history);
    }

    /// <summary>Anula (bypass) ou reativa uma zona.</summary>
    public async Task<HistoryDto> AlterarBypassAsync(BypassComandoDto dto)
    {
        var anular = dto.Acao.Equals("Anular", StringComparison.OrdinalIgnoreCase);
        if (!anular && !dto.Acao.Equals("Reativar", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Ação inválida. Valores aceitos: Anular, Reativar.");
        }

        var (central, adapter) = await ObterCentralEAdapterAsync(dto.CentralId);
        var zonas = ExigirRecurso<IZonaAdapter>(adapter, central, "bypass de zonas");

        _logger.LogInformation("Enviando {Acao} zona {Zona} para Central {CentralId} ({Fabricante})",
            dto.Acao, dto.Zona, central.Id, central.Fabricante);

        var resultado = anular
            ? await zonas.BypassZona(central.IP, central.Porta, central.Usuario, central.Senha, dto.Zona)
            : await zonas.CancelarBypassZona(central.IP, central.Porta, central.Usuario, central.Senha, dto.Zona);

        var history = await RegistrarAsync(central.Id, 0, $"{(anular ? "Anular" : "Reativar")} zona {dto.Zona}", resultado);
        return ParaDto(history);
    }

    public async Task<IEnumerable<ParticaoDto>> ConsultarParticoesAsync(int centralId)
    {
        var (central, adapter) = await ObterCentralEAdapterAsync(centralId);
        var particoes = ExigirRecurso<IParticaoAdapter>(adapter, central, "consulta de partições");

        try
        {
            var lista = await particoes.ConsultarParticoes(central.IP, central.Porta, central.Usuario, central.Senha);
            return lista.Select(p => new ParticaoDto { Numero = p.Numero, Estado = p.Estado.ToString(), EmAlarme = p.EmAlarme }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessException(ex.Message, statusCode: 409);
        }
    }

    public async Task<IEnumerable<ZonaDto>> ConsultarZonasAsync(int centralId)
    {
        var (central, adapter) = await ObterCentralEAdapterAsync(centralId);
        var zonas = ExigirRecurso<IZonaAdapter>(adapter, central, "consulta de zonas");

        try
        {
            var lista = await zonas.ConsultarZonas(central.IP, central.Porta, central.Usuario, central.Senha);
            return lista.Select(z => new ZonaDto { Numero = z.Numero, Status = z.Status.ToString(), Descricao = z.Descricao }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessException(ex.Message, statusCode: 409);
        }
        catch (NotImplementedException ex)
        {
            throw new BusinessException(ex.Message, statusCode: 501);
        }
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
