using CentralHub.SDK.Adapters.Jfl;
using CentralHub.SDK.Tests.Simulation;
using Xunit;

namespace CentralHub.SDK.Tests.Jfl;

public class JflProtocolLogTests
{
    [Fact]
    public void Descreve_capturas_reais_em_portugues()
    {
        var (k1, t1) = JflFrameDescriber.Describe(new JflFrame(RealCaptures.Bytes(RealCaptures.Connection)), JflLogDirection.Rx);
        Assert.Equal(JflLogKind.Conexao, k1);
        Assert.Contains("serie 0000000000", t1);
        Assert.Contains("0xA0 (Active 32 Duo)", t1);

        var (k2, t2) = JflFrameDescriber.Describe(new JflFrame(RealCaptures.Bytes(RealCaptures.EventZoneAlarm1130)), JflLogDirection.Rx);
        Assert.Equal(JflLogKind.Evento, k2);
        Assert.Contains("1130 Disparo da zona", t2);

        var (k3, t3) = JflFrameDescriber.Describe(new JflFrame(RealCaptures.Bytes(RealCaptures.CmdPgm2On)), JflLogDirection.Tx);
        Assert.Equal(JflLogKind.Comando, k3);
        Assert.Equal("0x50 Ligar PGM 2", t3);

        var (k4, t4) = JflFrameDescriber.Describe(new JflFrame(RealCaptures.Bytes(RealCaptures.StatusAfterPgmOn)), JflLogDirection.Rx);
        Assert.Equal(JflLogKind.Status, k4);
        Assert.Contains("PGMs ligadas: 2", t4);

        var (k5, t5) = JflFrameDescriber.Describe(new JflFrame(RealCaptures.Bytes(RealCaptures.CmdBypassZone9)), JflLogDirection.Tx);
        Assert.Equal(JflLogKind.Comando, k5);
        Assert.Contains("zonas 9", t5);
    }

    [Fact]
    public void Registro_e_circular()
    {
        var log = new JflProtocolLog(capacity: 10);
        for (var i = 0; i < 25; i++) log.Add("S", "r", JflLogDirection.Sistema, JflLogKind.Conexao, $"m{i}");

        var all = log.Snapshot();
        Assert.Equal(10, all.Count);
        Assert.Equal("m24", all[^1].Description);
        Assert.Equal(2, log.Snapshot(afterId: all[^3].Id).Count); // leitura incremental
    }

    [Fact]
    public async Task Servidor_registra_rx_tx_ack_evento_e_desconexao()
    {
        await using var h = new ServerHarness();
        var sim = JflPanelSimulator.Active100Bus(h.Port);
        await h.ConnectAsync(sim);
        await sim.SendEventAsync("1130", 1, 3);
        await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Event);
        await h.Adapter.AcionarPGM("", 0, sim.Serial, "", 1);
        await sim.DisposeAsync();
        await h.WaitUntilAsync(() => h.Registry.Log.Snapshot().Any(e => e.Kind == JflLogKind.Desconexao));

        var log = h.Registry.Log.Snapshot();
        Assert.Contains(log, e => e.Direction == JflLogDirection.Rx && e.Kind == JflLogKind.Conexao && e.Hex!.StartsWith("7B66"));
        Assert.Contains(log, e => e.Direction == JflLogDirection.Tx && e.Kind == JflLogKind.Ack && e.Description.StartsWith("ACK de conexao: aceita"));
        Assert.Contains(log, e => e.Direction == JflLogDirection.Sistema && e.Description.StartsWith("Central identificada: Active 100 Bus"));
        Assert.Contains(log, e => e.Kind == JflLogKind.Evento && e.Description.Contains("1130"));
        Assert.Contains(log, e => e.Kind == JflLogKind.Ack && e.Description.StartsWith("ACK de evento"));
        Assert.Contains(log, e => e.Direction == JflLogDirection.Tx && e.Description == "0x50 Ligar PGM 1");
        Assert.Contains(log, e => e.Kind == JflLogKind.Status && e.Description.Contains("eco 0x50"));
        Assert.All(log.Where(e => e.Serial is not null), e => Assert.Equal(sim.Serial, e.Serial));
    }

    [Fact]
    public async Task Timeout_de_comando_e_reconexao_aparecem_no_log()
    {
        await using var h = new ServerHarness(configureAdapter: o => o.CommandTimeout = TimeSpan.FromMilliseconds(300));
        await using var first = JflPanelSimulator.Active20(h.Port);
        await h.ConnectAsync(first);
        first.Mute = true;
        await h.Adapter.AcionarPGM("", 0, first.Serial, "", 1);

        await using var second = JflPanelSimulator.Active20(h.Port);
        await second.ConnectAsync();
        await h.WaitUntilAsync(() => h.Registry.Log.Snapshot().Any(e => e.Kind == JflLogKind.Reconexao));

        var log = h.Registry.Log.Snapshot();
        Assert.Contains(log, e => e.Kind == JflLogKind.Timeout && e.Description.Contains("0x4D") || e.Kind == JflLogKind.Timeout && e.Description.Contains("0x50"));
        Assert.Contains(log, e => e.Kind == JflLogKind.Reconexao);
    }
}
