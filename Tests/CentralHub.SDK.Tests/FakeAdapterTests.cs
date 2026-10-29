using CentralHub.SDK.Adapters;
using Xunit;

namespace CentralHub.SDK.Tests;

public class FakeAdapterTests
{
    private readonly FakeAdapter _adapter = new();

    [Fact]
    public async Task Conectar_RetornaVerdadeiro()
    {
        Assert.True(await _adapter.Conectar("127.0.0.1", 9009, "admin", "senha"));
    }

    [Fact]
    public async Task VerificarConectividade_RetornaCentralSimuladaOnline()
    {
        var resultado = await _adapter.VerificarConectividade("127.0.0.1", 9009, "admin", "senha");

        Assert.True(resultado.Sucesso);
        Assert.Equal("Fake", resultado.Fabricante);
        Assert.Equal("Online", resultado.Status);
    }

    [Fact]
    public async Task AcionarPGM_InformaOPgmLigado()
    {
        var resultado = await _adapter.AcionarPGM("127.0.0.1", 9009, "admin", "senha", 3);

        Assert.True(resultado.Sucesso);
        Assert.Equal("PGM 3 ligado", resultado.Resultado);
    }

    [Fact]
    public async Task DesligarPGM_InformaOPgmDesligado()
    {
        var resultado = await _adapter.DesligarPGM("127.0.0.1", 9009, "admin", "senha", 3);

        Assert.True(resultado.Sucesso);
        Assert.Equal("PGM 3 desligado", resultado.Resultado);
    }

    [Fact]
    public async Task PulsoPGM_InformaOTempoDoPulso()
    {
        var resultado = await _adapter.PulsoPGM("127.0.0.1", 9009, "admin", "senha", 2, 500);

        Assert.True(resultado.Sucesso);
        Assert.Equal("PGM 2 pulso de 500ms", resultado.Resultado);
    }
}
