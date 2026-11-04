using System.Diagnostics;
using CentralHub.SDK.Adapters.Jfl;

namespace CentralHub.SDK.Adapters;

public class JflAdapterOptions
{
    /// <summary>Tempo para a central responder a um comando com o quadro de status.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>Idade maxima do ultimo status para ser reutilizado sem novo pedido.</summary>
    public TimeSpan StatusMaxAge { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Apos arme/desarme, quanto esperar o estado da particao mudar (tempo de saida, por exemplo).</summary>
    public TimeSpan StateVerifyTimeout { get; set; } = TimeSpan.FromSeconds(6);

    public TimeSpan StatePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Teto de um pulso de PGM por software (o pulso e ligar + esperar + desligar).</summary>
    public TimeSpan MaxPulse { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Modo somente leitura: recusa qualquer comando que altere a central (PGM, arme, bypass).</summary>
    public bool ReadOnly { get; set; }
}

/// <summary>
/// Adapter das centrais JFL Active. Diferente dos adapters TCP-cliente, a central JFL e quem conecta no
/// servidor (<see cref="JflServer"/>); aqui os comandos saem pela conexao ativa da central, localizada
/// pelo NUMERO DE SERIE, que na Central cadastrada fica no campo Usuario.
///
/// Nao e herdeiro de <see cref="TcpAdapterBase"/> de proposito: conectar no IP da central nao funciona e
/// uma porta aberta nao prova nada. IP/porta/senha da Central sao ignorados (comandos sem autenticacao;
/// a familia 0x37 com senha nao e usada por causa do bloqueio apos 5 senhas erradas).
///
/// Modelos: apenas Active 100 Bus, Active 20 e Active 32 Duo tem comandos habilitados
/// (<see cref="JflModelProfile"/>); os dois primeiros ainda nao foram validados em hardware.
/// </summary>
public class JflAdapter : ICentralAdapter, IZonaAdapter, IParticaoAdapter
{
    /// <summary>Porta padrão em que o servidor JFL do CentralHub escuta as centrais.</summary>
    public const int PortaPadrao = 9090;

    private readonly JflSessionRegistry _registry;
    private readonly JflAdapterOptions _options;

    /// <summary>
    /// Opcoes usadas pelo construtor sem parametros (o criado por <see cref="AdapterFactory"/>).
    /// O host (API) as define na inicializacao a partir da configuracao.
    /// </summary>
    public static JflAdapterOptions DefaultOptions { get; set; } = new();

    public JflAdapter() : this(JflSessionRegistry.Default, DefaultOptions)
    {
    }

    public JflAdapter(JflSessionRegistry registry, JflAdapterOptions? options = null)
    {
        _registry = registry;
        _options = options ?? new JflAdapterOptions();
    }

    // ------------------------------------------------------------------ conectividade

    public Task<bool> Conectar(string ip, int porta, string usuario, string senha)
        => Task.FromResult(_registry.Find(usuario) is not null);

    public async Task<ConexaoResult> VerificarConectividade(string ip, int porta, string usuario, string senha)
    {
        var session = _registry.Find(usuario);
        if (session?.Info is null || session.Profile is null)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Fabricante = "JFL",
                Status = "Offline",
                Erro = NaoConectada(usuario)
            };
        }

        var timer = Stopwatch.StartNew();
        try
        {
            await session.SendAsync(JflEncoder.StatusRequest, _options.CommandTimeout).ConfigureAwait(false);
            timer.Stop();
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return new ConexaoResult
            {
                Sucesso = false,
                Fabricante = "JFL",
                Modelo = session.Profile.Description,
                Firmware = session.Info.Firmware,
                Status = "Offline",
                Erro = $"Central conectada, mas sem resposta ao pedido de status: {ex.Message}"
            };
        }

        return new ConexaoResult
        {
            Sucesso = true,
            Fabricante = "JFL",
            Modelo = session.Profile.Description,
            Firmware = session.Info.Firmware,
            Status = "Online",
            LatenciaMs = timer.ElapsedMilliseconds
        };
    }

    // ------------------------------------------------------------------ PGM

    public Task<ComandoResult> AcionarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => DefinirPgmAsync(usuario, pgm, ligar: true);

    public Task<ComandoResult> DesligarPGM(string ip, int porta, string usuario, string senha, int pgm)
        => DefinirPgmAsync(usuario, pgm, ligar: false);

    /// <summary>
    /// Pulso por SOFTWARE: liga, espera e desliga. O protocolo nao tem comando de pulso; se o CentralHub
    /// parar entre os dois comandos a PGM fica ligada (a menos que esteja programada "sem retencao", caso
    /// em que a propria central a desliga). Por isso o desligamento e tentado sempre.
    /// </summary>
    public async Task<ComandoResult> PulsoPGM(string ip, int porta, string usuario, string senha, int pgm, int tempoMs)
    {
        if (tempoMs <= 0 || TimeSpan.FromMilliseconds(tempoMs) > _options.MaxPulse)
        {
            return Falha($"Tempo de pulso invalido: use de 1 ms ate {_options.MaxPulse.TotalMilliseconds:0} ms.");
        }

        var ligar = await DefinirPgmAsync(usuario, pgm, ligar: true).ConfigureAwait(false);
        if (!ligar.Sucesso) return ligar;

        ComandoResult desligar;
        try
        {
            await Task.Delay(tempoMs).ConfigureAwait(false);
        }
        finally
        {
            desligar = await DefinirPgmAsync(usuario, pgm, ligar: false).ConfigureAwait(false);
        }

        return desligar.Sucesso
            ? Ok($"PGM {pgm}: pulso de {tempoMs} ms (ligada e desligada por software, confirmado pelo status da central)")
            : Falha($"PGM {pgm} foi ligada, mas NAO foi confirmado o desligamento: {desligar.Resultado}. Verifique a saida no local.");
    }

    private async Task<ComandoResult> DefinirPgmAsync(string usuario, int pgm, bool ligar)
    {
        var (ctx, erro) = ObterContexto(usuario, "PGM");
        if (ctx is null) return Falha(erro!);
        if (pgm < 1 || pgm > ctx.Perfil.Pgms)
        {
            return Falha($"PGM {pgm} fora do limite do modelo {ctx.Perfil.Name}: 1 a {ctx.Perfil.Pgms}.");
        }

        try
        {
            var status = await ObterStatusAsync(ctx.Sessao).ConfigureAwait(false);
            if (!status.PgmPermissionUnknown(pgm) && !status.IsPgmPermitted(pgm))
            {
                return Falha($"A central nao permite operar a PGM {pgm} remotamente: ela nao esta programada com funcao " +
                             "'acionada pelo usuario' (com/sem retencao). Ajuste a programacao da PGM na central.");
            }

            var resposta = await ctx.Sessao
                .SendAsync(seq => ligar ? JflEncoder.PgmOn(seq, pgm) : JflEncoder.PgmOff(seq, pgm), _options.CommandTimeout)
                .ConfigureAwait(false);

            if (resposta.IsPgmOn(pgm) == ligar)
            {
                return Ok($"PGM {pgm} {(ligar ? "ligada" : "desligada")} (confirmado pelo status da central){Tag(ctx)}");
            }

            return Falha($"A central respondeu, mas a PGM {pgm} continua {(ligar ? "desligada" : "ligada")}. " +
                         $"Verifique a funcao da PGM na programacao.{Tag(ctx)}");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return Falha($"Sem confirmacao da central: {ex.Message}{Tag(ctx)}");
        }
    }

    // ------------------------------------------------------------------ particoes

    public async Task<IEnumerable<ParticaoResult>> ConsultarParticoes(string ip, int porta, string usuario, string senha)
    {
        var ctx = ObterContextoOuLancar(usuario);
        var status = await ObterStatusOuLancarAsync(ctx.Sessao).ConfigureAwait(false);

        var result = new List<ParticaoResult>();
        for (var i = 0; i < Math.Min(ctx.Perfil.Partitions, status.Partitions.Count); i++)
        {
            var p = status.Partitions[i];
            if (!p.Programmed) continue;
            result.Add(new ParticaoResult { Numero = i + 1, Estado = Estado(p), EmAlarme = p.InAlarm });
        }
        return result;
    }

    public Task<ComandoResult> ArmarParticao(string ip, int porta, string usuario, string senha, int particao)
        => AlterarParticaoAsync(usuario, particao, armar: true);

    public Task<ComandoResult> DesarmarParticao(string ip, int porta, string usuario, string senha, int particao)
        => AlterarParticaoAsync(usuario, particao, armar: false);

    private async Task<ComandoResult> AlterarParticaoAsync(string usuario, int particao, bool armar)
    {
        var (ctx, erro) = ObterContexto(usuario, "arme/desarme");
        if (ctx is null) return Falha(erro!);
        if (particao < 1 || particao > ctx.Perfil.Partitions)
        {
            return Falha($"Particao {particao} fora do limite do modelo {ctx.Perfil.Name}: 1 a {ctx.Perfil.Partitions}.");
        }

        try
        {
            var status = await ObterStatusAsync(ctx.Sessao).ConfigureAwait(false);
            if (particao > status.Partitions.Count || !status.Partitions[particao - 1].Programmed)
            {
                return Falha($"A particao {particao} nao esta programada nesta central.");
            }

            var resposta = await ctx.Sessao
                .SendAsync(seq => armar ? JflEncoder.Arm(seq, particao) : JflEncoder.Disarm(seq, particao), _options.CommandTimeout)
                .ConfigureAwait(false);

            // O arme pode ter tempo de saida e pode ser recusado (zona aberta). Confirma pelo estado real.
            var limite = DateTime.UtcNow + _options.StateVerifyTimeout;
            var atual = resposta;
            while (true)
            {
                var estado = atual.Partitions[particao - 1];
                if (armar ? estado.Armed : estado.Disarmed)
                {
                    return Ok($"Particao {particao} {(armar ? "armada" : "desarmada")} (confirmado pelo status da central){Tag(ctx)}");
                }

                if (DateTime.UtcNow >= limite) break;
                await Task.Delay(_options.StatePollInterval).ConfigureAwait(false);
                atual = await ctx.Sessao.SendAsync(JflEncoder.StatusRequest, _options.CommandTimeout).ConfigureAwait(false);
            }

            var observado = Estado(atual.Partitions[particao - 1]);
            return Falha($"Comando enviado, mas a particao {particao} continua '{observado}'. " +
                         "Possiveis causas: zona aberta, usuario/permissao do monitoramento restrita na central ou tempo de saida ainda em andamento. " +
                         $"Consulte o status/eventos.{Tag(ctx)}");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return Falha($"Sem confirmacao da central: {ex.Message}{Tag(ctx)}");
        }
    }

    // ------------------------------------------------------------------ zonas

    public async Task<IEnumerable<ZonaResult>> ConsultarZonas(string ip, int porta, string usuario, string senha)
    {
        var ctx = ObterContextoOuLancar(usuario);
        var status = await ObterStatusOuLancarAsync(ctx.Sessao).ConfigureAwait(false);

        return status.Zones
            .Where(z => z.Number <= ctx.Perfil.Zones)
            .Select(z => new ZonaResult
            {
                Numero = z.Number,
                Status = z.Status switch
                {
                    JflZoneStatus.Open => ZonaStatus.Aberta,
                    JflZoneStatus.Closed => ZonaStatus.Fechada,
                    JflZoneStatus.Triggered => ZonaStatus.Disparo,
                    JflZoneStatus.Bypassed => ZonaStatus.Bypass,
                    _ => ZonaStatus.Problema
                },
                Descricao = z.Status switch
                {
                    JflZoneStatus.Tamper => "Tamper",
                    JflZoneStatus.ShortCircuit => "Curto-circuito",
                    JflZoneStatus.LowBattery => "Bateria fraca",
                    JflZoneStatus.NotCommunicating => "Sem comunicacao",
                    _ => null
                }
            })
            .ToList();
    }

    public Task<ComandoResult> BypassZona(string ip, int porta, string usuario, string senha, int zona)
        => AlterarBypassAsync(usuario, zona, anular: true);

    public Task<ComandoResult> CancelarBypassZona(string ip, int porta, string usuario, string senha, int zona)
        => AlterarBypassAsync(usuario, zona, anular: false);

    /// <summary>
    /// O comando 0x52 SUBSTITUI o conjunto de zonas anuladas; por isso o conjunto atual e lido do status
    /// e reenviado com a zona alterada. Zonas auto-anuladas nao aparecem como anuladas no status e
    /// podem ser afetadas (comportamento nao documentado).
    /// </summary>
    private async Task<ComandoResult> AlterarBypassAsync(string usuario, int zona, bool anular)
    {
        var (ctx, erro) = ObterContexto(usuario, "bypass");
        if (ctx is null) return Falha(erro!);
        if (zona < 1 || zona > ctx.Perfil.Zones)
        {
            return Falha($"Zona {zona} fora do limite do modelo {ctx.Perfil.Name}: 1 a {ctx.Perfil.Zones}.");
        }

        try
        {
            var status = await ObterStatusAsync(ctx.Sessao).ConfigureAwait(false);
            var existente = status.Zones.FirstOrDefault(z => z.Number == zona);
            if (existente is null)
            {
                return Falha($"A zona {zona} nao esta habilitada nesta central.");
            }

            if (anular && !existente.Bypassable)
            {
                return Falha($"A zona {zona} nao permite inibicao (atributo 'permite inibir' desligado na programacao).");
            }

            var conjunto = status.BypassedZones.ToHashSet();
            if (anular) conjunto.Add(zona); else conjunto.Remove(zona);

            var resposta = await ctx.Sessao
                .SendAsync(seq => JflEncoder.Bypass(seq, conjunto), _options.CommandTimeout)
                .ConfigureAwait(false);

            var agora = resposta.Zones.FirstOrDefault(z => z.Number == zona);
            var anulada = agora?.Status == JflZoneStatus.Bypassed;
            return anulada == anular
                ? Ok($"Zona {zona} {(anular ? "anulada (bypass)" : "reativada")} (confirmado pelo status da central){Tag(ctx)}")
                : Falha($"A central respondeu, mas a zona {zona} nao ficou {(anular ? "anulada" : "reativada")}.{Tag(ctx)}");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            return Falha($"Sem confirmacao da central: {ex.Message}{Tag(ctx)}");
        }
    }

    // ------------------------------------------------------------------ auxiliares

    private sealed record Contexto(JflSession Sessao, JflModelProfile Perfil);

    private (Contexto? Ctx, string? Erro) ObterContexto(string usuario, string operacao)
    {
        var session = _registry.Find(usuario);
        if (session?.Info is null || session.Profile is null)
        {
            return (null, NaoConectada(usuario));
        }

        if (_options.ReadOnly)
        {
            return (null, $"CentralHub em modo somente leitura: a operacao de {operacao} foi bloqueada.");
        }

        if (!session.Profile.CommandsEnabled)
        {
            return (null, $"Modelo {session.Profile.Description} (byte 0x{session.Profile.ModelByte:X2}) nao tem comandos habilitados " +
                          "no CentralHub: so ha suporte validado/implementado para Active 100 Bus e Active 20 (e Active 32 Duo).");
        }

        return (new Contexto(session, session.Profile), null);
    }

    private Contexto ObterContextoOuLancar(string usuario)
    {
        var session = _registry.Find(usuario);
        if (session?.Info is null || session.Profile is null)
        {
            throw new InvalidOperationException(NaoConectada(usuario));
        }
        return new Contexto(session, session.Profile);
    }

    private async Task<JflStatus> ObterStatusAsync(JflSession session)
    {
        if (session.LastStatus is { } cached
            && session.LastStatusAtUtc is { } at
            && DateTime.UtcNow - at <= _options.StatusMaxAge)
        {
            return cached;
        }
        return await session.SendAsync(JflEncoder.StatusRequest, _options.CommandTimeout).ConfigureAwait(false);
    }

    private async Task<JflStatus> ObterStatusOuLancarAsync(JflSession session)
    {
        try
        {
            return await ObterStatusAsync(session).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
        {
            throw new InvalidOperationException($"Sem resposta da central: {ex.Message}", ex);
        }
    }

    private string NaoConectada(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return "Informe o numero de serie da central JFL no campo Usuario da Central.";
        }

        var porta = _registry.ListenPort is { } p ? $" (porta {p})" : "";
        return $"A central JFL '{serial.Trim()}' nao esta conectada ao servidor do CentralHub{porta}. " +
               "A central JFL e quem conecta: programe o IP do servidor nos enderecos 702/703 e a porta nos 706/707, " +
               "habilite o reporte por Ethernet (endereco 700, tecla 3) e confira o firewall.";
    }

    private static ParticaoEstado Estado(JflPartitionState p) =>
        !p.Programmed ? ParticaoEstado.NaoProgramada
        : p.ArmedStay ? ParticaoEstado.ArmadaStay
        : p.ArmedAway ? ParticaoEstado.ArmadaTotal
        : ParticaoEstado.Desarmada;

    private static string Tag(Contexto ctx) =>
        ctx.Perfil.HardwareValidated ? "" : " [modelo ainda nao validado em hardware]";

    private static ComandoResult Ok(string texto) => new() { Sucesso = true, Resultado = texto };

    private static ComandoResult Falha(string texto) => new() { Sucesso = false, Resultado = texto };
}
