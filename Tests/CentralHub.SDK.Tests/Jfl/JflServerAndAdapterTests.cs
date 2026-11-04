using CentralHub.SDK.Adapters;
using CentralHub.SDK.Adapters.Jfl;
using CentralHub.SDK.Tests.Simulation;
using Xunit;

namespace CentralHub.SDK.Tests.Jfl;

/// <summary>
/// Servidor + adapter ponta a ponta contra o SIMULADOR de central (nao e hardware nem captura).
/// Provam o comportamento do CentralHub (acks, registro por serie, reconexao, confirmacao pelo status,
/// limites por modelo); NAO provam compatibilidade com Active 100 Bus / Active 20 reais.
/// </summary>
public sealed class ServerHarness : IAsyncDisposable
{
    public JflSessionRegistry Registry { get; } = new();
    public JflServer Server { get; }
    public JflAdapter Adapter { get; }
    public List<EventoCentral> Events { get; } = new();
    public List<JflSession> Connected { get; } = new();

    public ServerHarness(Action<JflServerOptions>? configureServer = null, Action<JflAdapterOptions>? configureAdapter = null, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        var so = new JflServerOptions { Port = 0, BindAddress = "127.0.0.1" };
        configureServer?.Invoke(so);
        Server = new JflServer(so, Registry, logger);
        Server.EventoRecebido += e => { lock (Events) Events.Add(e); };
        Server.PanelConnected += s => { lock (Connected) Connected.Add(s); };

        var ao = new JflAdapterOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(2),
            StateVerifyTimeout = TimeSpan.FromMilliseconds(400),
            StatePollInterval = TimeSpan.FromMilliseconds(100),
            StatusMaxAge = TimeSpan.Zero // sempre status fresco nos testes
        };
        configureAdapter?.Invoke(ao);
        Adapter = new JflAdapter(Registry, ao);

        Server.Start();
    }

    public int Port => Server.BoundPort;

    public async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("Condicao nao atendida a tempo.");
    }

    public async Task<JflPanelSimulator> ConnectAsync(JflPanelSimulator sim)
    {
        await sim.ConnectAsync();
        await WaitUntilAsync(() => Registry.Find(sim.Serial) is not null);
        // aguarda o status inicial pedido pelo servidor ser respondido
        await WaitUntilAsync(() => Registry.Find(sim.Serial)!.LastStatus is not null);
        return sim;
    }

    public async ValueTask DisposeAsync() => await Server.DisposeAsync();
}

public class JflServerTests
{
    [Fact]
    public async Task Aceita_central_registra_por_serie_e_confirma_a_conexao_ecoando_o_seq()
    {
        await using var h = new ServerHarness();
        await using var sim = Active100Bus(h);
        await h.ConnectAsync(sim);

        Assert.True(sim.ConnectionAccepted);
        var ack = await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Connection);
        Assert.Equal(0xF9, ack.Seq);                                   // ecoa o SEQ da central
        Assert.Equal(new byte[] { 0x01, 0x05 }, ack.Payload.ToArray()); // aceita + keep-alive 5 min

        var session = h.Registry.Find(sim.Serial)!;
        Assert.Equal("Active 100 Bus", session.Profile!.Name);
        Assert.Equal("700", session.Info!.Firmware);
        Assert.NotNull(session.LastStatus); // o servidor pede o primeiro status sozinho
        Assert.Single(h.Connected);
    }

    [Fact]
    public async Task Evento_e_confirmado_com_o_contador_original_e_entregue_mapeado()
    {
        await using var h = new ServerHarness();
        await using var sim = Active100Bus(h);
        await h.ConnectAsync(sim);

        var counter = new byte[] { 0x00, 0x00, 0x39, 0x1D };
        await sim.SendEventAsync("1130", partition: 3, subject: 12, seq: 0x52, counter: counter);

        var ack = await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Event);
        Assert.Equal(0x52, ack.Seq);
        Assert.Equal(new byte[] { 0x01 }.Concat(counter).ToArray(), ack.Payload.ToArray());

        await h.WaitUntilAsync(() => { lock (h.Events) return h.Events.Count == 1; });
        var evento = h.Events[0];
        Assert.Equal(TipoEvento.Alarme, evento.Tipo);
        Assert.Equal(12, evento.Zona);
        Assert.Equal(3, evento.Particao);
        Assert.Equal(sim.Serial, evento.Serial);
        Assert.Equal("1130", evento.Codigo);
        Assert.NotNull(evento.Bruto);
    }

    [Fact]
    public async Task Evento_entregue_byte_a_byte_gera_uma_confirmacao_e_um_evento()
    {
        await using var h = new ServerHarness();
        await using var sim = Active100Bus(h);
        await h.ConnectAsync(sim);

        foreach (var b in RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130))
        {
            await sim.SendRawAsync(new[] { b });
        }

        await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Event);
        await h.WaitUntilAsync(() => { lock (h.Events) return h.Events.Count == 1; });
        await Task.Delay(100);
        Assert.Equal(1, sim.Count(f => f.Cmd == JflCmd.Event));
        Assert.Single(h.Events);
    }

    [Fact]
    public async Task Evento_com_codigo_desconhecido_ainda_e_confirmado()
    {
        await using var h = new ServerHarness();
        await using var sim = Active20(h);
        await h.ConnectAsync(sim);

        await sim.SendEventAsync("1999", 1, 1);

        await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Event);
        await h.WaitUntilAsync(() => { lock (h.Events) return h.Events.Count == 1; });
        Assert.Equal(TipoEvento.Geral, h.Events[0].Tipo);
    }

    [Fact]
    public async Task Registro_de_quadros_brutos_loga_rx_e_tx_em_hexadecimal_quando_habilitado()
    {
        var logger = new ListLogger();
        await using var h = new ServerHarness(o => o.LogRawFrames = true, logger: logger);
        await using var sim = Active20(h);
        await h.ConnectAsync(sim);

        var lines = logger.Lines();
        Assert.Contains(lines, l => l.StartsWith("rx ") && l.Contains("7B66F921"));   // quadro 0x21 recebido
        Assert.Contains(lines, l => l.StartsWith("tx ") && l.Contains("7B07F9210105")); // ack 0x21 enviado
    }

    [Fact]
    public async Task Sem_a_opcao_nao_ha_log_de_quadros_brutos()
    {
        var logger = new ListLogger();
        await using var h = new ServerHarness(logger: logger);
        await using var sim = Active20(h);
        await h.ConnectAsync(sim);

        Assert.DoesNotContain(logger.Lines(), l => l.StartsWith("rx ") || l.StartsWith("tx "));
    }

    [Fact]
    public async Task Keepalive_da_central_e_confirmado_com_o_intervalo_configurado()
    {
        await using var h = new ServerHarness(o => o.KeepAliveMinutes = 3);
        await using var sim = Active20(h);
        await h.ConnectAsync(sim);

        await sim.SendKeepAliveAsync(0x07);

        var ack = await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.KeepAlive);
        Assert.Equal(0x07, ack.Seq);
        Assert.Equal(new byte[] { 3 }, ack.Payload.ToArray());
    }

    [Fact]
    public async Task Reconexao_da_mesma_central_substitui_a_sessao_anterior()
    {
        await using var h = new ServerHarness();
        await using var first = Active100Bus(h);
        await h.ConnectAsync(first);
        var oldSession = h.Registry.Find(first.Serial)!;

        await using var second = Active100Bus(h); // mesmo serie, novo IP/porta de origem
        await second.ConnectAsync();
        await h.WaitUntilAsync(() => !ReferenceEquals(h.Registry.Find(first.Serial), oldSession));

        Assert.True(await first.WaitForCloseAsync());      // a conexao antiga foi encerrada
        Assert.False(oldSession.IsConnected);
        Assert.True(h.Registry.Find(first.Serial)!.IsConnected);
        Assert.Single(h.Registry.Sessions);
    }

    [Fact]
    public async Task Queda_da_conexao_remove_a_central_do_registro()
    {
        await using var h = new ServerHarness();
        var sim = Active20(h);
        await h.ConnectAsync(sim);

        await sim.DisposeAsync();

        await h.WaitUntilAsync(() => h.Registry.Find(sim.Serial) is null);
    }

    [Fact]
    public async Task Central_silenciosa_e_derrubada_pelo_watchdog_para_rediscar()
    {
        await using var h = new ServerHarness(o =>
        {
            o.IdleFloor = TimeSpan.FromMilliseconds(300);
            o.KeepAliveMinutes = 0; // 2,5 x 0 => vale o piso
        });
        await using var sim = Active20(h);
        await sim.ConnectAsync();
        await h.WaitUntilAsync(() => h.Registry.Find(sim.Serial) is not null);

        Assert.True(await sim.WaitForCloseAsync(3000));
        await h.WaitUntilAsync(() => h.Registry.Find(sim.Serial) is null);
    }

    [Fact]
    public async Task Central_nao_autorizada_e_recusada_quando_configurado()
    {
        await using var h = new ServerHarness(o =>
        {
            o.AcceptUnknownPanels = false;
            o.AcceptPanel = serial => serial == "2000000001";
        });
        await using var denied = Active100Bus(h, serial: "9999999999");
        await denied.ConnectAsync();

        Assert.False(denied.ConnectionAccepted);
        Assert.True(await denied.WaitForCloseAsync());
        Assert.Null(h.Registry.Find("9999999999"));

        await using var allowed = Active20(h, serial: "2000000001");
        await h.ConnectAsync(allowed);
        Assert.True(allowed.ConnectionAccepted);
    }

    [Theory]
    [InlineData(0xB3)]
    [InlineData(0x7A)]
    public async Task Geracao_de_protocolo_nao_suportada_encerra_a_conexao_sem_registrar(byte header)
    {
        await using var h = new ServerHarness();
        await using var sim = new JflPanelSimulator(h.Port, "3000000001", 0xA4);
        await sim.ConnectNoHandshakeAsync();
        await sim.SendRawAsync(new byte[] { header, 0x10, 0x01, 0x02, 0x03 });

        Assert.True(await sim.WaitForCloseAsync());
        Assert.Empty(h.Registry.Sessions);
    }

    [Fact]
    public async Task Modelo_desconhecido_conecta_e_gera_eventos_mas_nao_aceita_comandos()
    {
        await using var h = new ServerHarness();
        await using var sim = new JflPanelSimulator(h.Port, "4000000001", 0x99);
        sim.Partitions[0] = 1;
        await h.ConnectAsync(sim);
        await sim.SendEventAsync("1130", 1, 1);
        await h.WaitUntilAsync(() => { lock (h.Events) return h.Events.Count == 1; });

        var result = await h.Adapter.AcionarPGM("", 0, sim.Serial, "", 1);

        Assert.False(result.Sucesso);
        Assert.Contains("nao tem comandos habilitados", result.Resultado);
        Assert.Equal(0, sim.Count(f => f.Cmd == JflCmd.PgmOn));
    }

    private static JflPanelSimulator Active100Bus(ServerHarness h, string serial = "1000000001") => JflPanelSimulator.Active100Bus(h.Port, serial);
    private static JflPanelSimulator Active20(ServerHarness h, string serial = "2000000001") => JflPanelSimulator.Active20(h.Port, serial);
}

public class JflAdapterTests
{
    private const string Bus = "1000000001";
    private const string A20 = "2000000001";

    // ---------------- conectividade ----------------

    [Fact]
    public async Task Central_nao_conectada_fica_offline_com_orientacao_clara()
    {
        await using var h = new ServerHarness();

        var r = await h.Adapter.VerificarConectividade("10.0.0.5", 9090, Bus, "");

        Assert.False(r.Sucesso);
        Assert.Equal("Offline", r.Status);
        Assert.Contains("702/703", r.Erro);
        Assert.Contains("706/707", r.Erro);
    }

    [Fact]
    public async Task Sem_serie_informado_pede_o_campo_usuario()
    {
        await using var h = new ServerHarness();
        var r = await h.Adapter.VerificarConectividade("10.0.0.5", 9090, "", "");
        Assert.False(r.Sucesso);
        Assert.Contains("numero de serie", r.Erro);
    }

    [Fact]
    public async Task Central_conectada_fica_online_com_modelo_firmware_e_aviso_de_nao_validado()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));

        var r = await h.Adapter.VerificarConectividade("ignorado", 0, Bus, "ignorada");

        Assert.True(r.Sucesso);
        Assert.Equal("Online", r.Status);
        Assert.Equal("JFL", r.Fabricante);
        Assert.Equal("Active 100 Bus (PCI-350) [nao validado em hardware]", r.Modelo);
        Assert.Equal("700", r.Firmware);
        Assert.True(r.LatenciaMs >= 0);
    }

    [Fact]
    public async Task Central_que_para_de_responder_a_status_e_reportada_offline()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active20(h.Port));
        sim.Mute = true;

        var r = await h.Adapter.VerificarConectividade("", 0, A20, "");

        Assert.False(r.Sucesso);
        Assert.Contains("sem resposta", r.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fabrica_identifica_JFL_pela_sessao_ativa_e_nunca_cai_no_FakeAdapter()
    {
        // O registro padrao e compartilhado: usa um serie exclusivo deste teste.
        await using var server = new JflServer(new JflServerOptions { Port = 0, BindAddress = "127.0.0.1" });
        server.Start();
        await using var sim = JflPanelSimulator.Active100Bus(server.BoundPort, "7700000001");
        await sim.ConnectAsync();
        await WaitAsync(() => JflSessionRegistry.Default.Find("7700000001") is not null);

        var detected = await AdapterFactory.DetectarEConectar("127.0.0.1", 1, "7700000001", "");

        Assert.True(detected.Sucesso);
        Assert.Equal("JFL", detected.Fabricante);
        Assert.NotEqual("Fake", detected.Fabricante);
    }

    // ---------------- PGM ----------------

    [Fact]
    public async Task Active_100_Bus_liga_e_desliga_a_pgm_confirmando_pelo_status()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));

        var on = await h.Adapter.AcionarPGM("", 0, Bus, "", 12);
        Assert.True(on.Sucesso, on.Resultado);
        Assert.Contains("PGM 12 ligada", on.Resultado);
        Assert.Contains("[modelo ainda nao validado em hardware]", on.Resultado);
        Assert.NotEqual(0, sim.PgmState & (1 << 11));
        var sent = sim.Received.Single(f => f.Cmd == JflCmd.PgmOn);
        Assert.Equal(new byte[] { 12 }, sent.Payload.ToArray());   // PGM 9..16 usa o mesmo comando (o bitmap alto e do status)

        var off = await h.Adapter.DesligarPGM("", 0, Bus, "", 12);
        Assert.True(off.Sucesso, off.Resultado);
        Assert.Equal(0, sim.PgmState);
    }

    [Fact]
    public async Task Active_20_recusa_pgm_acima_do_teto_do_modelo_sem_enviar_nada()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active20(h.Port));

        var ok = await h.Adapter.AcionarPGM("", 0, A20, "", 4);
        var tooHigh = await h.Adapter.AcionarPGM("", 0, A20, "", 5);   // Active 20: PGM 1..4

        Assert.True(ok.Sucesso, ok.Resultado);
        Assert.False(tooHigh.Sucesso);
        Assert.Contains("1 a 4", tooHigh.Resultado);
        Assert.Equal(1, sim.Count(f => f.Cmd == JflCmd.PgmOn));
    }

    [Fact]
    public async Task Pgm_sem_funcao_acionada_pelo_usuario_e_recusada_antes_de_enviar()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active100Bus(h.Port);
        sim.PgmPermitted = 0b10; // so a PGM 2 aceita comando remoto
        await using var _ = await h.ConnectAsync(sim);

        var r = await h.Adapter.AcionarPGM("", 0, Bus, "", 3);

        Assert.False(r.Sucesso);
        Assert.Contains("acionada pelo usuario", r.Resultado);
        Assert.Equal(0, sim.Count(f => f.Cmd == JflCmd.PgmOn));
    }

    [Fact]
    public async Task Comando_sem_resposta_da_central_nao_e_reportado_como_sucesso()
    {
        await using var h = new ServerHarness(configureAdapter: o => o.CommandTimeout = TimeSpan.FromMilliseconds(400));
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));
        sim.Mute = true;

        var r = await h.Adapter.AcionarPGM("", 0, Bus, "", 1);

        Assert.False(r.Sucesso);
        Assert.Contains("Sem confirmacao", r.Resultado);
    }

    [Fact]
    public async Task Pgm_cujo_estado_nao_muda_na_resposta_e_falha()
    {
        // Status em cache diz "permitida"; a central passa a ignorar o comando (estado nunca muda).
        await using var h = new ServerHarness(configureAdapter: o => o.StatusMaxAge = TimeSpan.FromMinutes(5));
        var sim = JflPanelSimulator.Active20(h.Port);
        await using var _ = await h.ConnectAsync(sim);
        sim.PgmPermitted = 0;

        var r = await h.Adapter.AcionarPGM("", 0, A20, "", 1);

        Assert.False(r.Sucesso);
        Assert.Contains("continua desligada", r.Resultado);
    }

    [Fact]
    public async Task Pulso_liga_espera_e_desliga_por_software()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));

        var r = await h.Adapter.PulsoPGM("", 0, Bus, "", 2, 150);

        Assert.True(r.Sucesso, r.Resultado);
        Assert.Contains("pulso de 150 ms", r.Resultado);
        Assert.Equal(0, sim.PgmState);
        var cmds = sim.Received.Where(f => f.Cmd is JflCmd.PgmOn or JflCmd.PgmOff).Select(f => f.Cmd).ToArray();
        Assert.Equal(new[] { JflCmd.PgmOn, JflCmd.PgmOff }, cmds);
    }

    [Fact]
    public async Task Pulso_invalido_e_recusado()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));

        Assert.False((await h.Adapter.PulsoPGM("", 0, Bus, "", 2, 0)).Sucesso);
        Assert.False((await h.Adapter.PulsoPGM("", 0, Bus, "", 2, (int)TimeSpan.FromHours(1).TotalMilliseconds)).Sucesso);
        Assert.Equal(0, sim.Count(f => f.Cmd == JflCmd.PgmOn));
    }

    [Fact]
    public async Task Modo_somente_leitura_bloqueia_todos_os_comandos()
    {
        await using var h = new ServerHarness(configureAdapter: o => o.ReadOnly = true);
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));

        Assert.False((await h.Adapter.AcionarPGM("", 0, Bus, "", 1)).Sucesso);
        Assert.False((await h.Adapter.ArmarParticao("", 0, Bus, "", 1)).Sucesso);
        Assert.False((await h.Adapter.BypassZona("", 0, Bus, "", 1)).Sucesso);
        Assert.Equal(0, sim.Count(f => f.Cmd is JflCmd.PgmOn or JflCmd.Arm or JflCmd.Bypass));
        // leitura continua permitida
        Assert.NotEmpty(await h.Adapter.ConsultarZonas("", 0, Bus, ""));
    }

    // ---------------- particoes ----------------

    [Fact]
    public async Task Active_100_Bus_tem_16_particoes_e_Active_20_somente_2()
    {
        await using var h = new ServerHarness();
        await using var bus = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));
        await using var a20 = await h.ConnectAsync(JflPanelSimulator.Active20(h.Port));

        Assert.Equal(16, (await h.Adapter.ConsultarParticoes("", 0, Bus, "")).Count());
        Assert.Equal(2, (await h.Adapter.ConsultarParticoes("", 0, A20, "")).Count());

        Assert.True((await h.Adapter.ArmarParticao("", 0, Bus, "", 16)).Sucesso);
        var denied = await h.Adapter.ArmarParticao("", 0, A20, "", 3);
        Assert.False(denied.Sucesso);
        Assert.Contains("1 a 2", denied.Resultado);
    }

    [Fact]
    public async Task Arme_e_desarme_sao_confirmados_pelo_estado_real_da_particao()
    {
        await using var h = new ServerHarness();
        await using var sim = await h.ConnectAsync(JflPanelSimulator.Active20(h.Port));

        var arm = await h.Adapter.ArmarParticao("", 0, A20, "", 1);
        Assert.True(arm.Sucesso, arm.Resultado);
        Assert.Equal(0x02, sim.Partitions[0]);
        Assert.Equal(ParticaoEstado.ArmadaTotal, (await h.Adapter.ConsultarParticoes("", 0, A20, "")).First().Estado);

        var disarm = await h.Adapter.DesarmarParticao("", 0, A20, "", 1);
        Assert.True(disarm.Sucesso, disarm.Resultado);
        Assert.Equal(0x01, sim.Partitions[0]);
    }

    [Fact]
    public async Task Arme_recusado_pela_central_e_reportado_como_falha()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active20(h.Port);
        sim.RefuseArm = true; // ex.: zona aberta
        await using var _ = await h.ConnectAsync(sim);

        var r = await h.Adapter.ArmarParticao("", 0, A20, "", 1);

        Assert.False(r.Sucesso);
        Assert.Contains("continua 'Desarmada'", r.Resultado);
    }

    [Fact]
    public async Task Particao_nao_programada_e_recusada()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active100Bus(h.Port);
        sim.Partitions[4] = 0; // particao 5 nao programada
        await using var _ = await h.ConnectAsync(sim);

        var r = await h.Adapter.ArmarParticao("", 0, Bus, "", 5);

        Assert.False(r.Sucesso);
        Assert.Contains("nao esta programada", r.Resultado);
    }

    // ---------------- zonas e bypass ----------------

    [Fact]
    public async Task Zonas_refletem_o_teto_de_cada_modelo()
    {
        await using var h = new ServerHarness();
        await using var bus = await h.ConnectAsync(JflPanelSimulator.Active100Bus(h.Port));
        await using var a20 = await h.ConnectAsync(JflPanelSimulator.Active20(h.Port));

        Assert.Equal(99, (await h.Adapter.ConsultarZonas("", 0, Bus, "")).Count());
        Assert.Equal(32, (await h.Adapter.ConsultarZonas("", 0, A20, "")).Count());
    }

    [Fact]
    public async Task Zona_aberta_e_zona_com_tamper_sao_mapeadas()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active20(h.Port);
        sim.Zones[3] = (byte)JflZoneStatus.Open;
        sim.Zones[4] = (byte)JflZoneStatus.Tamper;
        sim.Zones[5] = (byte)JflZoneStatus.Triggered;
        await using var _ = await h.ConnectAsync(sim);

        var zones = (await h.Adapter.ConsultarZonas("", 0, A20, "")).ToDictionary(z => z.Numero);

        Assert.Equal(ZonaStatus.Fechada, zones[1].Status);
        Assert.Equal(ZonaStatus.Aberta, zones[3].Status);
        Assert.Equal(ZonaStatus.Problema, zones[4].Status);
        Assert.Equal("Tamper", zones[4].Descricao);
        Assert.Equal(ZonaStatus.Disparo, zones[5].Status);
    }

    [Fact]
    public async Task Bypass_envia_o_conjunto_completo_MSB_first_e_preserva_as_outras_zonas()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active100Bus(h.Port);
        sim.Bypassed.Add(2); // ja anulada
        await using var _ = await h.ConnectAsync(sim);

        var r = await h.Adapter.BypassZona("", 0, Bus, "", 9);

        Assert.True(r.Sucesso, r.Resultado);
        var sent = sim.Received.Last(f => f.Cmd == JflCmd.Bypass);
        Assert.Equal(13, sent.Payload.Length);
        Assert.Equal(new byte[] { 0x40, 0x80 }, sent.Payload[..2].ToArray()); // zonas 2 e 9, MSB-first
        Assert.Equal(new[] { 2, 9 }, sim.Bypassed.OrderBy(x => x));

        var cancel = await h.Adapter.CancelarBypassZona("", 0, Bus, "", 9);
        Assert.True(cancel.Sucesso, cancel.Resultado);
        Assert.Equal(new[] { 2 }, sim.Bypassed);
    }

    [Fact]
    public async Task Bypass_de_zona_que_nao_permite_inibir_ou_inexistente_e_recusado()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active20(h.Port);
        sim.Bypassable.Remove(7);
        await using var _ = await h.ConnectAsync(sim);

        var notAllowed = await h.Adapter.BypassZona("", 0, A20, "", 7);
        var tooHigh = await h.Adapter.BypassZona("", 0, A20, "", 33);

        Assert.False(notAllowed.Sucesso);
        Assert.Contains("permite inibicao", notAllowed.Resultado);
        Assert.False(tooHigh.Sucesso);
        Assert.Equal(0, sim.Count(f => f.Cmd == JflCmd.Bypass));
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !condition()) await Task.Delay(10);
        Assert.True(condition());
    }
}

public class AdapterFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Marca Qualquer")]
    public async Task Fabricante_nao_identificado_nunca_vira_FakeAdapter(string? fabricante)
    {
        Assert.Equal(FabricanteTipo.NaoIdentificado, AdapterFactory.ResolverPorNome(fabricante));

        var adapter = AdapterFactory.Criar(AdapterFactory.ResolverPorNome(fabricante));
        var r = await adapter.AcionarPGM("1.1.1.1", 1, "u", "s", 1);

        Assert.IsType<NaoIdentificadoAdapter>(adapter);
        Assert.False(r.Sucesso);
        Assert.Contains("Testar Conex", r.Resultado);
    }

    [Fact]
    public void Fake_somente_quando_explicitamente_pedido()
    {
        Assert.Equal(FabricanteTipo.Fake, AdapterFactory.ResolverPorNome("fake"));
        Assert.IsType<FakeAdapter>(AdapterFactory.Criar(FabricanteTipo.Fake));
        Assert.IsType<JflAdapter>(AdapterFactory.Criar(AdapterFactory.ResolverPorNome(" JFL ")));
        Assert.IsType<IntelbrasAdapter>(AdapterFactory.Criar(AdapterFactory.ResolverPorNome("Intelbras")));
    }
}

/// <summary>Logger de teste que guarda as mensagens formatadas.</summary>
public sealed class ListLogger : Microsoft.Extensions.Logging.ILogger
{
    private readonly List<string> _lines = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lines) _lines.Add(formatter(state, exception));
    }

    public List<string> Lines()
    {
        lock (_lines) return _lines.ToList();
    }
}
