using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using CentralHub.SDK.Adapters.Jfl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralHub.Api.Controllers;

/// <summary>
/// Diagnóstico das centrais JFL: servidor de monitoramento, sessões conectadas, status lido da central,
/// perfis de modelo e log de protocolo (RX/TX). Os comandos continuam em /api/operation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JflController : ControllerBase
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(5);

    private readonly AppDbContext _context;
    private readonly JflOptions _options;
    private readonly JflSessionRegistry _registry = JflSessionRegistry.Default;

    public JflController(AppDbContext context, IOptions<JflOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    [HttpGet("servidor")]
    public ActionResult<JflServidorDto> Servidor() => Ok(new JflServidorDto
    {
        Habilitado = _options.Enabled,
        PortaEmUso = _registry.ListenPort,
        PortaConfigurada = _options.Port,
        BindAddress = _options.BindAddress,
        KeepAliveMinutes = _options.KeepAliveMinutes,
        SomenteLeitura = _options.ReadOnly,
        AceitaCentraisDesconhecidas = _options.AcceptUnknownPanels,
        LogQuadrosBrutos = _options.LogRawFrames,
        SessoesAtivas = _registry.Sessions.Count(s => s.IsConnected && s.Info is not null),
        CapacidadeLog = _registry.Log.Capacity
    });

    [HttpGet("modelos")]
    public ActionResult<IEnumerable<JflModeloDto>> Modelos() =>
        Ok(JflModelProfile.All.OrderByDescending(p => p.CommandsEnabled).ThenBy(p => p.Name).Select(ParaDto));

    [HttpGet("sessoes")]
    public async Task<ActionResult<IEnumerable<JflSessaoDto>>> Sessoes()
    {
        var sessoes = _registry.Sessions.Where(s => s.IsConnected && s.Info is not null && s.Profile is not null).ToList();
        var seriais = sessoes.Select(s => s.Serial!).ToList();
        var centrais = await _context.Centrals.AsNoTracking()
            .Where(c => seriais.Contains(c.Usuario) && c.Fabricante != null && c.Fabricante.ToLower() == "jfl")
            .Select(c => new { c.Id, c.Nome, c.Usuario })
            .ToListAsync();

        return Ok(sessoes.OrderBy(s => s.Serial).Select(s =>
        {
            var central = centrais.FirstOrDefault(c => string.Equals(c.Usuario, s.Serial, StringComparison.OrdinalIgnoreCase));
            return new JflSessaoDto
            {
                Serial = s.Serial!,
                Origem = s.RemoteEndPoint,
                ConectadaEm = s.ConnectedAtUtc,
                UltimoQuadroEm = s.LastFrameAtUtc,
                UltimoStatusEm = s.LastStatusAtUtc,
                Firmware = s.Info!.Firmware,
                Mac = s.Info.Mac,
                Imei = s.Info.Imei,
                Sinal = s.Info.Signal,
                Modelo = ParaDto(s.Profile!),
                CentralId = central?.Id,
                CentralNome = central?.Nome
            };
        }));
    }

    /// <summary>
    /// Status da central. Com <c>atualizar=true</c> pede um novo quadro de status à central (comando 0x4D);
    /// se ela não responder, devolve o último recebido marcado como não atualizado.
    /// </summary>
    [HttpGet("sessoes/{serial}/status")]
    public async Task<ActionResult<JflStatusDto>> Status(string serial, [FromQuery] bool atualizar = true)
    {
        var session = _registry.Find(serial);
        if (session?.Profile is null)
        {
            return NotFound(new { status = 404, mensagem = $"A central JFL '{serial}' não está conectada ao servidor do CentralHub." });
        }

        var status = session.LastStatus;
        var atualizado = false;
        string? aviso = null;
        if (atualizar)
        {
            try
            {
                status = await session.SendAsync(JflEncoder.StatusRequest, StatusTimeout);
                atualizado = true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
            {
                aviso = $"A central não respondeu agora ({ex.Message}). Exibindo o último status recebido.";
            }
        }

        if (status is null)
        {
            return Conflict(new { status = 409, mensagem = aviso ?? "Nenhum status recebido desta central ainda." });
        }

        var p = session.Profile;
        return Ok(new JflStatusDto
        {
            Serial = session.Serial!,
            LidoEm = session.LastStatusAtUtc,
            Atualizado = atualizado,
            Aviso = aviso,
            Relogio = status.Clock,
            BateriaVolts = Math.Round(status.BatteryVolts, 2),
            Eletrificador = Eletrificador(status.FenceRaw),
            TamanhoQuadro = status.FrameLength,
            Particoes = status.Partitions.Take(p.Partitions).Select((x, i) => new JflParticaoStatusDto
            {
                Numero = i + 1,
                Programada = x.Programmed,
                Estado = !x.Programmed ? "NaoProgramada" : x.ArmedStay ? "ArmadaStay" : x.ArmedAway ? "ArmadaTotal" : x.Disarmed ? "Desarmada" : $"Desconhecido (0x{x.Raw:X2})",
                EmAlarme = x.InAlarm,
                Bruto = $"0x{x.Raw:X2}"
            }).ToList(),
            Zonas = status.Zones.Where(z => z.Number <= p.Zones).Select(z => new JflZonaStatusDto
            {
                Numero = z.Number,
                Estado = z.Status.ToString(),
                PermiteInibir = z.Bypassable
            }).ToList(),
            Pgms = Enumerable.Range(1, p.Pgms).Select(n => new JflPgmStatusDto
            {
                Numero = n,
                Ligada = status.IsPgmOn(n),
                Permitida = status.PgmPermissionUnknown(n) ? null : status.IsPgmPermitted(n)
            }).ToList(),
            Problemas = status.Problems.Select(Problema).ToList()
        });
    }

    /// <summary>Log de protocolo. Filtros combináveis; <c>desdeId</c> permite leitura incremental.</summary>
    [HttpGet("log")]
    public ActionResult<IEnumerable<JflLogDto>> Log(
        [FromQuery] string? serial, [FromQuery] string? direcao, [FromQuery] string? tipo,
        [FromQuery] string? texto, [FromQuery] long desdeId = 0, [FromQuery] int limite = 500)
    {
        limite = Math.Clamp(limite, 1, 5000);
        IEnumerable<JflLogEntry> q = _registry.Log.Snapshot(desdeId);
        if (!string.IsNullOrWhiteSpace(serial)) q = q.Where(e => string.Equals(e.Serial, serial.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(direcao)) q = q.Where(e => e.Direction.ToString().Equals(direcao, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(e => e.Kind.ToString().Equals(tipo, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(e => e.Description.Contains(t, StringComparison.OrdinalIgnoreCase)
                             || (e.Hex?.Contains(t.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Ok(q.TakeLast(limite).Select(e => new JflLogDto
        {
            Id = e.Id,
            DataHora = DateTime.SpecifyKind(e.TimestampUtc, DateTimeKind.Utc),
            Serial = e.Serial,
            Origem = e.Remote,
            Direcao = e.Direction.ToString(),
            Tipo = e.Kind.ToString(),
            Comando = e.Cmd is { } c ? $"0x{c:X2}" : null,
            Hex = e.Hex,
            Descricao = e.Description
        }));
    }

    [HttpDelete("log")]
    public IActionResult LimparLog()
    {
        _registry.Log.Clear();
        return NoContent();
    }

    private static JflModeloDto ParaDto(JflModelProfile p) => new()
    {
        ByteModelo = $"0x{p.ModelByte:X2}",
        Nome = p.Name,
        Placa = p.Board,
        Particoes = p.Partitions,
        Zonas = p.Zones,
        Pgms = p.Pgms,
        Eletrificador = p.HasFence,
        ComandosHabilitados = p.CommandsEnabled,
        ValidadoEmHardware = p.HardwareValidated,
        Evidencia = p.Evidence
    };

    private static string Eletrificador(byte raw) => raw switch
    {
        0x00 => "Não programado",
        0x01 => "Desarmado",
        0x02 => "Armado",
        0x04 => "Desarmado, não pronto",
        0x81 => "Desarmado, em alarme",
        0x82 => "Armado, em alarme",
        0x84 => "Desarmado, pronto, em alarme",
        _ => $"Desconhecido (0x{raw:X2})"
    };

    private static JflProblemaDto Problema(JflProblem p)
    {
        var (descricao, grupo) = p switch
        {
            JflProblem.WirelessLowBattery => ("Bateria fraca em dispositivo sem fio", "Dispositivos"),
            JflProblem.SensorSupervision => ("Falha de supervisão de sensor", "Dispositivos"),
            JflProblem.AuxiliaryOutput => ("Problema na saída auxiliar", "Central"),
            JflProblem.Tamper => ("Tamper", "Dispositivos"),
            JflProblem.Dhcp => ("Falha de DHCP", "Comunicação"),
            JflProblem.NetworkCable => ("Cabo de rede", "Comunicação"),
            JflProblem.CellularModule => ("Módulo de celular", "Comunicação"),
            JflProblem.Sms => ("SMS", "Comunicação"),
            JflProblem.Ethernet => ("Ethernet", "Comunicação"),
            JflProblem.Gprs => ("GPRS", "Comunicação"),
            JflProblem.TelephoneLine => ("Linha telefônica", "Comunicação"),
            JflProblem.Short => ("Curto-circuito", "Central"),
            JflProblem.Keypad => ("Teclado", "Dispositivos"),
            JflProblem.Siren => ("Sirene", "Dispositivos"),
            JflProblem.Battery => ("Bateria da central", "Energia"),
            JflProblem.AcMains => ("Falta de energia AC", "Energia"),
            JflProblem.BatteryReversed => ("Bateria invertida", "Energia"),
            JflProblem.DestinationIp2 => ("Destino IP 2", "Comunicação"),
            JflProblem.DestinationIp1 => ("Destino IP 1", "Comunicação"),
            JflProblem.DnsServer => ("Servidor DNS", "Comunicação"),
            JflProblem.KeypadAc => ("AC do teclado", "Dispositivos"),
            JflProblem.SirenSupervision => ("Supervisão da sirene", "Dispositivos"),
            JflProblem.WirelessPassword => ("Senha do Wi-Fi", "Comunicação"),
            JflProblem.WirelessAuthentication => ("Autenticação do Wi-Fi", "Comunicação"),
            JflProblem.SsidNotFound => ("Rede Wi-Fi (SSID) não encontrada", "Comunicação"),
            JflProblem.IpConflict => ("Conflito de IP", "Comunicação"),
            JflProblem.Bus => ("Barramento (Bus)", "Dispositivos"),
            JflProblem.Ddns => ("DDNS", "Comunicação"),
            JflProblem.Notification => ("Notificações", "Comunicação"),
            JflProblem.EthernetModule => ("Módulo Ethernet", "Comunicação"),
            JflProblem.SignalLevel => ("Nível de sinal", "Comunicação"),
            JflProblem.SimCard => ("SIM card", "Comunicação"),
            _ => (p.ToString(), "Outros")
        };
        return new JflProblemaDto { Codigo = p.ToString(), Descricao = descricao, Grupo = grupo };
    }
}
