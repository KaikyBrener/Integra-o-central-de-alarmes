using System.Net.Http.Json;
using System.Text.Json;
using CentralHub.Api.Data;
using CentralHub.SDK.Adapters.Jfl;
using CentralHub.SDK.Tests.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CentralHub.Api.Tests;

/// <summary>
/// API completa (controllers, servicos, banco SQLite, servidor JFL hospedado) contra o SIMULADOR de
/// central. Nao e hardware: valida a integracao entre as camadas do CentralHub.
/// </summary>
public sealed class JflApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "chave-de-teste-jfl";

    // O modelo do EF guarda o protector do primeiro contexto do processo; compartilha o provider com ApiFactory.
    private static readonly Microsoft.AspNetCore.DataProtection.IDataProtectionProvider DataProtection = ApiFactory.CompartilharDataProtection;

    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"centralhub-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={DbPath}");
        builder.UseSetting("ApiKey", ApiKey);
        builder.UseSetting("Jfl:Port", "0");
        builder.UseSetting("Jfl:BindAddress", "127.0.0.1");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            services.AddSingleton(DataProtection);
        });
    }

    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { DbPath, DbPath + "-wal", DbPath + "-shm" })
        {
            try { File.Delete(f); } catch { /* melhor esforco */ }
        }
    }
}

public class JflApiIntegrationTests : IClassFixture<JflApiFactory>
{
    private readonly JflApiFactory _factory;

    public JflApiIntegrationTests(JflApiFactory factory) => _factory = factory;

    private static async Task<int> PortAsync(HttpClient client)
    {
        // O host sobe o servidor JFL em segundo plano; aguarda o registro informar a porta efetiva.
        for (var i = 0; i < 100; i++)
        {
            if (JflSessionRegistry.Default.ListenPort is { } p && p > 0) return p;
            await Task.Delay(50);
        }
        throw new TimeoutException("Servidor JFL nao iniciou.");
    }

    private static async Task<JsonElement> PostAsync(HttpClient c, string url, object body, int expected = 200)
    {
        var r = await c.PostAsJsonAsync(url, body);
        var text = await r.Content.ReadAsStringAsync();
        Assert.True((int)r.StatusCode == expected, $"{url} -> {(int)r.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement;
    }

    [Fact]
    public async Task Fluxo_completo_Active_100_Bus_teste_de_conexao_comandos_zonas_eventos()
    {
        var client = _factory.CreateClient();
        var port = await PortAsync(client);
        const string serial = "1000001000";

        await using var sim = JflPanelSimulator.Active100Bus(port, serial);
        await sim.ConnectAsync();
        for (var i = 0; i < 100 && JflSessionRegistry.Default.Find(serial)?.LastStatus is null; i++) await Task.Delay(50);

        // 1) Predio + teste de conexao: o fabricante e identificado pela sessao JFL, nunca por simulacao
        var building = await PostAsync(client, "/api/building", new { nome = "Predio JFL", descricao = "teste" }, 201);
        var teste = await PostAsync(client, "/api/central/testar-conexao",
            new { ip = "10.0.0.9", porta = 9090, usuario = serial, senha = "-" });
        Assert.True(teste.GetProperty("sucesso").GetBoolean(), teste.ToString());
        Assert.Equal("JFL", teste.GetProperty("fabricante").GetString());
        Assert.Contains("Active 100 Bus", teste.GetProperty("modelo").GetString());

        // 2) Cadastro como o frontend faria (com o fabricante devolvido pelo teste)
        var central = await PostAsync(client, "/api/central", new
        {
            nome = "Active 100 Bus - sala tecnica",
            ip = "10.0.0.9",
            porta = 9090,
            usuario = serial,
            senha = "-",
            buildingId = building.GetProperty("id").GetInt32(),
            fabricante = "JFL"
        }, 201);
        var centralId = central.GetProperty("id").GetInt32();

        // 3) PGM: ligar, desligar e pulso -> histórico registra o resultado real
        var ligar = await PostAsync(client, "/api/operation/enviar", new { centralId, pgm = 3, comando = "Ligar" });
        Assert.Contains("PGM 3 ligada", ligar.GetProperty("resultado").GetString());
        Assert.NotEqual(0, sim.PgmState & 0b100);

        var desligar = await PostAsync(client, "/api/operation/enviar", new { centralId, pgm = 3, comando = "Desligar" });
        Assert.Equal(0, sim.PgmState);
        Assert.True(desligar.GetProperty("sucesso").GetBoolean());

        var pulso = await PostAsync(client, "/api/operation/enviar", new { centralId, pgm = 2, comando = "Pulso", tempoPulsoMs = 100 });
        Assert.Contains("pulso de 100 ms", pulso.GetProperty("resultado").GetString());

        // 4) Falha real fica marcada no histórico (nunca parece sucesso)
        var invalida = await PostAsync(client, "/api/operation/enviar", new { centralId, pgm = 17, comando = "Ligar" });
        Assert.False(invalida.GetProperty("sucesso").GetBoolean());

        // 5) Particoes, zonas, arme e bypass
        var particoes = await client.GetFromJsonAsync<JsonElement>($"/api/operation/particoes/{centralId}");
        Assert.Equal(16, particoes.GetArrayLength());
        var zonas = await client.GetFromJsonAsync<JsonElement>($"/api/operation/zonas/{centralId}");
        Assert.Equal(99, zonas.GetArrayLength());

        var arme = await PostAsync(client, "/api/operation/particao", new { centralId, particao = 4, acao = "Armar" });
        Assert.Contains("armada", arme.GetProperty("resultado").GetString());
        Assert.Equal(0x02, sim.Partitions[3]);

        var bypass = await PostAsync(client, "/api/operation/bypass", new { centralId, zona = 9, acao = "Anular" });
        Assert.True(bypass.GetProperty("sucesso").GetBoolean());
        Assert.Contains(9, sim.Bypassed);

        // 6) Evento recebido da central é confirmado, persistido e vinculado à Central cadastrada
        await sim.SendEventAsync("1130", partition: 1, subject: 7);
        await sim.WaitForFrameAsync(f => f.Cmd == JflCmd.Event);
        JsonElement eventos = default;
        for (var i = 0; i < 100; i++)
        {
            eventos = await client.GetFromJsonAsync<JsonElement>($"/api/event?centralId={centralId}");
            if (eventos.GetArrayLength() > 0) break;
            await Task.Delay(50);
        }
        Assert.Equal(1, eventos.GetArrayLength());
        var ev = eventos[0];
        Assert.Equal("1130", ev.GetProperty("codigo").GetString());
        Assert.Equal("Alarme", ev.GetProperty("tipo").GetString());
        Assert.Equal(7, ev.GetProperty("zona").GetInt32());
        Assert.Equal(centralId, ev.GetProperty("centralId").GetInt32());

        // 7) Historico completo
        var historico = await client.GetFromJsonAsync<JsonElement>($"/api/operation/historico?centralId={centralId}");
        Assert.True(historico.GetArrayLength() >= 6);
    }

    [Fact]
    public async Task Diagnostico_JFL_expoe_servidor_sessao_status_por_modelo_e_log()
    {
        var client = _factory.CreateClient();
        var port = await PortAsync(client);
        const string serial = "2000002000";

        await using var sim = JflPanelSimulator.Active20(port, serial);
        sim.Zones[5] = 7;            // zona 5 aberta
        sim.PgmPermitted = 0b0011;   // so PGM 1 e 2 aceitam comando remoto
        await sim.ConnectAsync();
        for (var i = 0; i < 100 && JflSessionRegistry.Default.Find(serial)?.LastStatus is null; i++) await Task.Delay(50);

        var servidor = await client.GetFromJsonAsync<JsonElement>("/api/jfl/servidor");
        Assert.Equal(port, servidor.GetProperty("portaEmUso").GetInt32());

        var sessoes = await client.GetFromJsonAsync<JsonElement>("/api/jfl/sessoes");
        var sessao = sessoes.EnumerateArray().Single(s => s.GetProperty("serial").GetString() == serial);
        Assert.Equal("Active 20", sessao.GetProperty("modelo").GetProperty("nome").GetString());
        Assert.Equal(32, sessao.GetProperty("modelo").GetProperty("zonas").GetInt32());
        Assert.False(sessao.GetProperty("modelo").GetProperty("validadoEmHardware").GetBoolean());

        var status = await client.GetFromJsonAsync<JsonElement>($"/api/jfl/sessoes/{serial}/status");
        Assert.True(status.GetProperty("atualizado").GetBoolean());
        Assert.Equal(2, status.GetProperty("particoes").GetArrayLength());   // limitado ao modelo
        Assert.Equal(32, status.GetProperty("zonas").GetArrayLength());
        Assert.Equal(4, status.GetProperty("pgms").GetArrayLength());
        Assert.Equal("Open", status.GetProperty("zonas").EnumerateArray().Single(z => z.GetProperty("numero").GetInt32() == 5).GetProperty("estado").GetString());
        var pgms = status.GetProperty("pgms").EnumerateArray().ToList();
        Assert.True(pgms[1].GetProperty("permitida").GetBoolean());
        Assert.False(pgms[2].GetProperty("permitida").GetBoolean());

        var log = await client.GetFromJsonAsync<JsonElement>($"/api/jfl/log?serial={serial}&direcao=Rx");
        Assert.Contains(log.EnumerateArray(), e => e.GetProperty("tipo").GetString() == "Conexao" && e.GetProperty("hex").GetString()!.StartsWith("7B66"));
        Assert.All(log.EnumerateArray(), e => Assert.Equal("Rx", e.GetProperty("direcao").GetString()));

        var naoConectada = await client.GetAsync("/api/jfl/sessoes/0000000999/status");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, naoConectada.StatusCode);
    }

    [Fact]
    public async Task Central_sem_fabricante_identificado_nunca_vira_sucesso_simulado()
    {
        var client = _factory.CreateClient();
        await PortAsync(client);

        var building = await PostAsync(client, "/api/building", new { nome = "Predio B" }, 201);
        var central = await PostAsync(client, "/api/central", new
        {
            nome = "Sem fabricante", ip = "10.0.0.20", porta = 9009, usuario = "x", senha = "y",
            buildingId = building.GetProperty("id").GetInt32()
        }, 201);

        var r = await PostAsync(client, "/api/operation/enviar",
            new { centralId = central.GetProperty("id").GetInt32(), pgm = 1, comando = "Ligar" });

        var texto = r.GetProperty("resultado").GetString();
        Assert.False(r.GetProperty("sucesso").GetBoolean());
        Assert.Contains("nao identificado", texto!.Replace("não", "nao"));
    }

    [Fact]
    public async Task Central_JFL_nao_conectada_retorna_orientacao_e_falha_registrada()
    {
        var client = _factory.CreateClient();
        await PortAsync(client);

        var building = await PostAsync(client, "/api/building", new { nome = "Predio C" }, 201);
        var central = await PostAsync(client, "/api/central", new
        {
            nome = "Active 20 offline", ip = "10.0.0.30", porta = 9090, usuario = "2000009999", senha = "-",
            buildingId = building.GetProperty("id").GetInt32(), fabricante = "JFL"
        }, 201);

        var r = await PostAsync(client, "/api/operation/enviar",
            new { centralId = central.GetProperty("id").GetInt32(), pgm = 1, comando = "Ligar" });

        var texto = r.GetProperty("resultado").GetString()!;
        Assert.False(r.GetProperty("sucesso").GetBoolean());
        Assert.Contains("702/703", texto);
    }

    [Fact]
    public async Task Banco_antigo_sem_tabela_de_eventos_e_atualizado_sem_perder_dados()
    {
        var path = Path.Combine(Path.GetTempPath(), $"centralhub-legacy-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;

            await using (var ctx = new AppDbContext(options, ApiFactory.CompartilharDataProtection))
            {
                ctx.Database.EnsureCreated();
                ctx.Buildings.Add(new CentralHub.Api.Models.Building { Nome = "Antigo" });
                ctx.SaveChanges();
                ctx.Database.ExecuteSqlRaw("DROP TABLE \"Events\";"); // banco criado antes do suporte a eventos
            }

            await using (var ctx = new AppDbContext(options, ApiFactory.CompartilharDataProtection))
            {
                SchemaUpdater.Aplicar(ctx);
                SchemaUpdater.Aplicar(ctx); // idempotente

                ctx.Eventos.Add(new CentralHub.Api.Models.CentralEvent { DataHora = DateTime.Now, Tipo = "Alarme", Codigo = "1130" });
                ctx.SaveChanges();

                Assert.Single(ctx.Eventos.ToList());
                Assert.Single(ctx.Buildings.ToList());
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch { /* melhor esforco */ }
        }
    }
}
