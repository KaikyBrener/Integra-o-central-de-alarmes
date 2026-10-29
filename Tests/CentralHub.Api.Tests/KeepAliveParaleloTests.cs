using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CentralHub.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CentralHub.Api.Tests;

public class KeepAliveParaleloTests
{
    private const int QuantidadeDeCentrais = 20;

    private static async Task<List<int>> CriarCentraisAsync(HttpClient client, int quantidade, string fabricante)
    {
        var predio = await client.PostAsJsonAsync("/api/building", new { nome = "Prédio" });
        var buildingId = (await predio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var ids = new List<int>();
        for (var i = 0; i < quantidade; i++)
        {
            var resposta = await client.PostAsJsonAsync("/api/central", new
            {
                nome = $"Central {i}", ip = "127.0.0.1", porta = 9009, usuario = "admin", senha = "senha", buildingId, fabricante
            });
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            ids.Add((await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
        }
        return ids;
    }

    [Fact]
    public async Task VerificaAsCentraisEmParalelo_ComLimite_EGravaOResultadoDeTodas()
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var ids = await CriarCentraisAsync(client, QuantidadeDeCentrais, ApiFactory.FabricanteLento);
        var keepAlive = new KeepAliveService(factory.Services, NullLogger<KeepAliveService>.Instance);

        var cronometro = Stopwatch.StartNew();
        await keepAlive.VerificarCentraisAsync(CancellationToken.None);
        cronometro.Stop();

        // Uma a uma levaria 20 x 300 ms = 6 s; com 10 vagas deve levar cerca de 2 x 300 ms.
        var sequencial = QuantidadeDeCentrais * ApiFactory.DelayDoAdapterLento;
        Assert.True(cronometro.Elapsed < sequencial / 2, $"Levou {cronometro.Elapsed}, esperado bem abaixo de {sequencial}.");

        Assert.InRange(factory.Concorrencia.Maximo, 2, KeepAliveService.MaxVerificacoesSimultaneas);

        foreach (var id in ids)
        {
            var central = await client.GetFromJsonAsync<JsonElement>($"/api/central/{id}");
            Assert.Equal("Online", central.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task CentralComFalhaNaVerificacao_NaoImpedeAtualizarAsDemais()
    {
        using var factory = new ApiFactory();
        var client = factory.CriarClient();
        var boas = await CriarCentraisAsync(client, 3, ApiFactory.FabricanteComSucesso);
        var comErro = await CriarCentraisAsync(client, 1, ApiFactory.FabricanteQueLancaExcecao);
        var keepAlive = new KeepAliveService(factory.Services, NullLogger<KeepAliveService>.Instance);

        await keepAlive.VerificarCentraisAsync(CancellationToken.None);

        foreach (var id in boas)
        {
            var central = await client.GetFromJsonAsync<JsonElement>($"/api/central/{id}");
            Assert.Equal("Online", central.GetProperty("status").GetString());
        }

        // A central que falhou mantém o último status conhecido (nunca verificada: nulo).
        var falha = await client.GetFromJsonAsync<JsonElement>($"/api/central/{comErro[0]}");
        Assert.Equal(JsonValueKind.Null, falha.GetProperty("status").ValueKind);
    }
}
