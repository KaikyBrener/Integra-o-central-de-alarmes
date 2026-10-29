using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CentralHub.Api.Tests;

public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private const string Senha = "Segredo-Muito-Unico-123";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CriarClient();
    }

    private async Task<int> CriarPredioAsync(string nome = "Prédio Teste")
    {
        var resposta = await _client.PostAsJsonAsync("/api/building", new { nome, descricao = "Prédio de teste" });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> CriarCentralAsync(int buildingId, string? fabricante = "Fake")
    {
        var resposta = await _client.PostAsJsonAsync("/api/central", new
        {
            nome = "Central Teste",
            ip = "127.0.0.1",
            porta = 9009,
            usuario = "admin",
            senha = Senha,
            buildingId,
            fabricante
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> CriarCentralNovaAsync(string? fabricante = "Fake") =>
        await CriarCentralAsync(await CriarPredioAsync(), fabricante);

    private Task<HttpResponseMessage> EnviarComandoAsync(object corpo) =>
        _client.PostAsJsonAsync("/api/operation/enviar", corpo);

    private static bool TemPropriedadeSenha(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Object => json.EnumerateObject().Any(p =>
            p.Name.Contains("senha", StringComparison.OrdinalIgnoreCase) || TemPropriedadeSenha(p.Value)),
        JsonValueKind.Array => json.EnumerateArray().Any(TemPropriedadeSenha),
        _ => false
    };

    // ---------- Cadastro ----------

    [Fact]
    public async Task CadastraPredioECentral_ECentralFicaConsultavel()
    {
        var buildingId = await CriarPredioAsync("Prédio Cadastro");
        var centralId = await CriarCentralAsync(buildingId);

        var resposta = await _client.GetAsync($"/api/central/{centralId}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var central = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Central Teste", central.GetProperty("nome").GetString());
        Assert.Equal(buildingId, central.GetProperty("buildingId").GetInt32());
        Assert.Equal("Prédio Cadastro", central.GetProperty("buildingNome").GetString());
    }

    [Fact]
    public async Task CadastrarCentralEmPredioInexistente_Retorna400()
    {
        var resposta = await _client.PostAsJsonAsync("/api/central", new
        {
            nome = "Órfã", ip = "127.0.0.1", porta = 9009, usuario = "admin", senha = Senha, buildingId = 987654
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // ---------- Senha ----------

    [Fact]
    public async Task RespostasDeCentral_NaoTemCampoSenhaNemOValorDaSenha()
    {
        var buildingId = await CriarPredioAsync();
        var criar = await _client.PostAsJsonAsync("/api/central", new
        {
            nome = "Central Sigilo", ip = "127.0.0.1", porta = 9009, usuario = "admin", senha = Senha, buildingId, fabricante = "Fake"
        });
        var corpoCriar = await criar.Content.ReadAsStringAsync();
        var id = JsonDocument.Parse(corpoCriar).RootElement.GetProperty("id").GetInt32();

        var corpoObter = await _client.GetStringAsync($"/api/central/{id}");
        var corpoListar = await _client.GetStringAsync("/api/central");

        foreach (var corpo in new[] { corpoCriar, corpoObter, corpoListar })
        {
            Assert.False(TemPropriedadeSenha(JsonDocument.Parse(corpo).RootElement), "A resposta não deve ter campo de senha.");
            Assert.DoesNotContain(Senha, corpo);
        }
    }

    [Fact]
    public async Task SenhaFicaCriptografadaNoBanco()
    {
        var centralId = await CriarCentralNovaAsync();

        var armazenada = _factory.LerSenhaArmazenada(centralId);

        Assert.NotEqual(Senha, armazenada);
        Assert.StartsWith("enc:v1:", armazenada);
        Assert.DoesNotContain(Senha, armazenada);
    }

    [Fact]
    public async Task SenhaCriptografada_ChegaAoAdapterEmTextoSimples_ECentralAtualizadaContinuaFuncionando()
    {
        var buildingId = await CriarPredioAsync();
        var centralId = await CriarCentralAsync(buildingId);

        var atualizar = await _client.PutAsJsonAsync($"/api/central/{centralId}", new
        {
            nome = "Central Renomeada", ip = "127.0.0.1", porta = 9009, usuario = "admin", senha = "Outra-Senha-456", buildingId, fabricante = "Fake"
        });
        Assert.Equal(HttpStatusCode.NoContent, atualizar.StatusCode);

        var armazenada = _factory.LerSenhaArmazenada(centralId);
        Assert.StartsWith("enc:v1:", armazenada);
        Assert.DoesNotContain("Outra-Senha-456", armazenada);

        var comando = await EnviarComandoAsync(new { centralId, pgm = 1, comando = "Ligar" });
        Assert.Equal(HttpStatusCode.OK, comando.StatusCode);
    }

    // ---------- Comandos ----------

    [Fact]
    public async Task ComandoValido_RegistraSucessoNoHistorico()
    {
        var centralId = await CriarCentralNovaAsync();

        var resposta = await EnviarComandoAsync(new { centralId, pgm = 3, comando = "Ligar" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var historico = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(historico.GetProperty("sucesso").GetBoolean());
        Assert.Equal("PGM 3 ligado", historico.GetProperty("resultado").GetString());
        Assert.Equal(3, historico.GetProperty("pgm").GetInt32());
        Assert.True(historico.GetProperty("data").GetDateTime().Kind == DateTimeKind.Utc);

        var lista = await _client.GetFromJsonAsync<JsonElement>($"/api/operation/historico?centralId={centralId}");
        var registro = Assert.Single(lista.EnumerateArray());
        Assert.True(registro.GetProperty("sucesso").GetBoolean());
        Assert.True(registro.GetProperty("data").GetDateTime().Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task ComandoRecusadoPelaCentral_NaoEhRegistradoComoSucesso()
    {
        var centralId = await CriarCentralNovaAsync(ApiFactory.FabricanteComFalha);

        var resposta = await EnviarComandoAsync(new { centralId, pgm = 1, comando = "Desligar" });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var historico = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(historico.GetProperty("sucesso").GetBoolean());
        Assert.Equal("Central recusou o comando", historico.GetProperty("resultado").GetString());

        var lista = await _client.GetFromJsonAsync<JsonElement>($"/api/operation/historico?centralId={centralId}");
        Assert.False(Assert.Single(lista.EnumerateArray()).GetProperty("sucesso").GetBoolean());
    }

    [Theory]
    [InlineData("Explodir", 1, 0)]
    [InlineData("Ligar", 0, 0)]
    [InlineData("Ligar", -2, 0)]
    [InlineData("Pulso", 1, 0)]
    public async Task ComandoInvalido_Retorna400_ENaoGravaHistorico(string comando, int pgm, int tempoPulsoMs)
    {
        var centralId = await CriarCentralNovaAsync();

        var resposta = await EnviarComandoAsync(new { centralId, pgm, comando, tempoPulsoMs });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var lista = await _client.GetFromJsonAsync<JsonElement>($"/api/operation/historico?centralId={centralId}");
        Assert.Equal(0, lista.GetArrayLength());
    }

    [Fact]
    public async Task ComandoParaCentralInexistente_Retorna404()
    {
        var resposta = await EnviarComandoAsync(new { centralId = 987654, pgm = 1, comando = "Ligar" });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task ConsultarCentralInexistente_Retorna404()
    {
        var resposta = await _client.GetAsync("/api/central/987654");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    // ---------- Chave de API ----------

    [Theory]
    [InlineData(null)]
    [InlineData("chave-errada")]
    [InlineData("")]
    public async Task SemChaveValida_Retorna401(string? chave)
    {
        var client = _factory.CriarClient(chave);

        var resposta = await client.GetAsync("/api/building");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task SemChave_NaoExecutaEscritas()
    {
        var client = _factory.CriarClient(chave: null);

        var resposta = await client.PostAsJsonAsync("/api/building", new { nome = "Intruso" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var predios = await _client.GetFromJsonAsync<JsonElement>("/api/building");
        Assert.DoesNotContain(predios.EnumerateArray(), p => p.GetProperty("nome").GetString() == "Intruso");
    }

    [Fact]
    public async Task PreflightDeCors_NaoExigeChave()
    {
        var client = _factory.CriarClient(chave: null);
        var requisicao = new HttpRequestMessage(HttpMethod.Options, "/api/building");
        requisicao.Headers.Add("Origin", "http://localhost:5173");
        requisicao.Headers.Add("Access-Control-Request-Method", "GET");
        requisicao.Headers.Add("Access-Control-Request-Headers", "x-api-key");

        var resposta = await client.SendAsync(requisicao);

        Assert.True(resposta.IsSuccessStatusCode);
    }

    // ---------- Teste de conexão sem falso sucesso ----------

    private static int PortaFechada()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var porta = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return porta;
    }

    [Fact]
    public async Task TestarConexao_ComCentralInacessivel_NaoDevolveSucesso()
    {
        var resposta = await _client.PostAsJsonAsync("/api/central/testar-conexao", new
        {
            ip = "127.0.0.1", porta = PortaFechada(), usuario = "admin", senha = Senha
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(resultado.GetProperty("sucesso").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(resultado.GetProperty("erro").GetString()));
    }

    [Fact]
    public async Task TestarConexao_ForaDeDesenvolvimento_IgnoraUsarAdapterFake()
    {
        // Mesmo com UsarAdapterFake=true, só o ambiente Development pode usar o fallback.
        using var factory = new ApiFactory("Production", usarAdapterFake: true);
        var client = factory.CriarClient();

        var resposta = await client.PostAsJsonAsync("/api/central/testar-conexao", new
        {
            ip = "127.0.0.1", porta = PortaFechada(), usuario = "admin", senha = Senha
        });

        var resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(resultado.GetProperty("sucesso").GetBoolean());
    }

    [Fact]
    public async Task TestarConexao_EmDesenvolvimentoComUsarAdapterFake_UsaOFallbackSimulado()
    {
        using var factory = new ApiFactory("Development", usarAdapterFake: true);
        var client = factory.CriarClient();

        var resposta = await client.PostAsJsonAsync("/api/central/testar-conexao", new
        {
            ip = "127.0.0.1", porta = PortaFechada(), usuario = "admin", senha = Senha
        });

        var resultado = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(resultado.GetProperty("sucesso").GetBoolean());
        Assert.Equal("Fake", resultado.GetProperty("fabricante").GetString());
    }
}
