using CentralHub.SDK.Adapters;
using Xunit;

namespace CentralHub.SDK.Tests;

public class AdapterFactoryTests
{
    [Theory]
    [InlineData("Intelbras", FabricanteTipo.Intelbras)]
    [InlineData("intelbras", FabricanteTipo.Intelbras)]
    [InlineData("  JFL ", FabricanteTipo.Jfl)]
    [InlineData("jfl", FabricanteTipo.Jfl)]
    [InlineData("Fake", FabricanteTipo.Fake)]
    [InlineData("Desconhecido", FabricanteTipo.Fake)]
    [InlineData("", FabricanteTipo.Fake)]
    [InlineData(null, FabricanteTipo.Fake)]
    public void ResolverPorNome_IgnoraMaiusculasEEspacos_ECaiNoFakeQuandoDesconhecido(string? nome, FabricanteTipo esperado)
    {
        Assert.Equal(esperado, AdapterFactory.ResolverPorNome(nome));
    }

    [Theory]
    [InlineData(FabricanteTipo.Intelbras, typeof(IntelbrasAdapter))]
    [InlineData(FabricanteTipo.Jfl, typeof(JflAdapter))]
    [InlineData(FabricanteTipo.Fake, typeof(FakeAdapter))]
    public void Criar_RetornaOAdapterDoFabricante(FabricanteTipo tipo, Type esperado)
    {
        Assert.IsType(esperado, AdapterFactory.Criar(tipo));
    }

    [Fact]
    public void PortasPadrao_SaoDistintasEntreFabricantes()
    {
        Assert.NotEqual(IntelbrasAdapter.PortaPadrao, JflAdapter.PortaPadrao);
    }
}
