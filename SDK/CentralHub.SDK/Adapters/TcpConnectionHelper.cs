using System.Diagnostics;
using System.Net.Sockets;

namespace CentralHub.SDK.Adapters;

/// <summary>
/// Helper interno usado pelos adapters para testar a conectividade TCP real
/// com a central (IP/Porta) e medir a latencia da conexao.
/// A identificacao de fabricante/modelo/firmware depende do protocolo
/// proprietario de cada central e deve ser implementada em cada adapter.
/// </summary>
internal static class TcpConnectionHelper
{
    private const int TimeoutMs = 3000;

    public static async Task<(bool sucesso, long latenciaMs, string? erro)> TestarConexao(string ip, int porta)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeoutMs);
        var cronometro = Stopwatch.StartNew();

        try
        {
            await client.ConnectAsync(ip, porta, cts.Token);
            cronometro.Stop();
            return (true, cronometro.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException)
        {
            cronometro.Stop();
            return (false, cronometro.ElapsedMilliseconds, "Tempo limite de conexao excedido");
        }
        catch (Exception ex)
        {
            cronometro.Stop();
            return (false, cronometro.ElapsedMilliseconds, ex.Message);
        }
    }
}
