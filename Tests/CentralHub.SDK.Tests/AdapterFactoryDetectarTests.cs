using System.Net;
using System.Net.Sockets;
using CentralHub.SDK.Adapters;
using Xunit;

namespace CentralHub.SDK.Tests;

public class AdapterFactoryDetectarTests
{
    /// <summary>Reserva uma porta livre e a libera, para que nada esteja escutando nela.</summary>
    private static int PortaFechada()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var porta = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return porta;
    }

    [Fact]
    public async Task SemAdapterRealConectado_ESemFallback_DevolveFalhaComErro()
    {
        var resultado = await AdapterFactory.DetectarEConectar("127.0.0.1", PortaFechada(), "admin", "senha");

        Assert.False(resultado.Sucesso);
        Assert.NotEqual("Fake", resultado.Fabricante);
        Assert.Equal("Offline", resultado.Status);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Erro));
    }

    [Fact]
    public async Task FallbackDesligadoExplicitamente_SeComportaComoPadrao()
    {
        var resultado = await AdapterFactory.DetectarEConectar(
            "127.0.0.1", PortaFechada(), "admin", "senha", permitirFallbackFake: false);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task SemAdapterRealConectado_ComFallbackPermitido_UsaOFakeAdapter()
    {
        var resultado = await AdapterFactory.DetectarEConectar(
            "127.0.0.1", PortaFechada(), "admin", "senha", permitirFallbackFake: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Fake", resultado.Fabricante);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComPortaAberta_ConectaPeloAdapterReal_SemUsarOFake(bool permitirFallbackFake)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var porta = ((IPEndPoint)listener.LocalEndpoint).Port;

            var resultado = await AdapterFactory.DetectarEConectar(
                "127.0.0.1", porta, "admin", "senha", permitirFallbackFake);

            Assert.True(resultado.Sucesso);
            Assert.NotEqual("Fake", resultado.Fabricante);
        }
        finally
        {
            listener.Stop();
        }
    }
}
