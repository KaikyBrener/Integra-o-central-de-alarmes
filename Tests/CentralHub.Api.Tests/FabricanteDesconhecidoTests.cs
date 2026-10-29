using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CentralHub.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralHub.Api.Tests;

/// <summary>
/// Central sem fabricante reconhecido (vazio, "Fake" ou desconhecido) não pode cair no
/// FakeAdapter, exceto em Development com UsarAdapterFake verdadeiro.
/// </summary>
public class FabricanteDesconhecidoTests
{
    private static async Task<int> CriarCentralAsync(HttpClient client, string? fabricante)
    {
        var predio = await client.PostAsJsonAsync("/api/building", new { nome = "Prédio" });
        var buildingId = (await predio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var central = await client.PostAsJsonAsync("/api/central", new
        {
            nome = "Central", ip = "127.0.0.1", porta = 9009, usuario = "admin", senha = "senha", buildingId, fabricante
        });
        Assert.Equal(HttpStatusCode.Created, central.StatusCode);
        return (await central.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client, int centralId) =>
        client.PostAsJsonAsync("/api/operation/enviar", new { centralId, pgm = 1, comando = "Ligar" });

    private static async Task AssertComandoRecusadoComoFabricanteDesconhecido(HttpClient client, int centralId)
    {
        var resposta = await EnviarAsync(client, centralId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("teste de conexão", corpo.GetProperty("mensagem").GetString());

        // Foi recusado antes de qualquer comando: nada vai para o histórico.
        var historico = await client.GetFromJsonAsync<JsonElement>($"/api/operation/historico?centralId={centralId}");
        Assert.Equal(0, historico.GetArrayLength());
    }

    private static async Task<JsonElement> ObterCentralAsync(HttpClient client, int centralId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/central/{centralId}");

    // ---------- Comandos ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Fake")]
    [InlineData("Marca Qualquer")]
    public async Task Comando_FabricanteNaoReconhecido_ForaDeDesenvolvimento_Retorna422(string? fabricante)
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante);

        await AssertComandoRecusadoComoFabricanteDesconhecido(client, centralId);
    }

    [Fact]
    public async Task Comando_EmDesenvolvimentoSemUsarAdapterFake_Retorna422()
    {
        using var factory = new ApiFactory("Development", usarAdapterFake: false);
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante: null);

        await AssertComandoRecusadoComoFabricanteDesconhecido(client, centralId);
    }

    [Fact]
    public async Task Comando_ForaDeDesenvolvimentoComUsarAdapterFake_Retorna422()
    {
        using var factory = new ApiFactory("Production", usarAdapterFake: true);
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante: "Fake");

        await AssertComandoRecusadoComoFabricanteDesconhecido(client, centralId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Fake")]
    public async Task Comando_EmDesenvolvimentoComUsarAdapterFake_UsaOFakeAdapter(string? fabricante)
    {
        using var factory = new ApiFactory("Development", usarAdapterFake: true);
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante);

        var resposta = await EnviarAsync(client, centralId);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var historico = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(historico.GetProperty("sucesso").GetBoolean());
        Assert.Equal("PGM 1 ligado", historico.GetProperty("resultado").GetString());
    }

    [Theory]
    [InlineData("Intelbras")]
    [InlineData("JFL")]
    public async Task Comando_FabricanteReconhecido_NaoDependeDoFake(string fabricante)
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante);

        var resposta = await EnviarAsync(client, centralId);

        // Os adapters reais ainda simulam o comando; o que se verifica aqui é que não são bloqueados.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // ---------- KeepAlive ----------

    private static KeepAliveService CriarKeepAlive(ApiFactory factory) =>
        new(factory.Services, NullLogger<KeepAliveService>.Instance);

    [Theory]
    [InlineData(null)]
    [InlineData("Fake")]
    [InlineData("Marca Qualquer")]
    public async Task KeepAlive_FabricanteNaoReconhecido_FicaDesconhecido_SemUsarOFake(string? fabricante)
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante);

        await CriarKeepAlive(factory).VerificarCentraisAsync(CancellationToken.None);

        var central = await ObterCentralAsync(client, centralId);
        Assert.Equal("Desconhecido", central.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, central.GetProperty("latencia").ValueKind);
    }

    [Fact]
    public async Task KeepAlive_EmDesenvolvimentoComUsarAdapterFake_UsaOFakeAdapter()
    {
        using var factory = new ApiFactory("Development", usarAdapterFake: true);
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, fabricante: null);

        await CriarKeepAlive(factory).VerificarCentraisAsync(CancellationToken.None);

        var central = await ObterCentralAsync(client, centralId);
        Assert.Equal("Online", central.GetProperty("status").GetString());
    }

    [Fact]
    public async Task KeepAlive_FabricanteReconhecido_AtualizaStatusPeloAdapter()
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var centralId = await CriarCentralAsync(client, ApiFactory.FabricanteComSucesso);

        await CriarKeepAlive(factory).VerificarCentraisAsync(CancellationToken.None);

        var central = await ObterCentralAsync(client, centralId);
        Assert.Equal("Online", central.GetProperty("status").GetString());
    }
}
