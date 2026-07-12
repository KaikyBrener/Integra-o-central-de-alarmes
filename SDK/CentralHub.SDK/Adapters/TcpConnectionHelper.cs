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
        var cronometro = Stopwatch.StartNew();

        try
        {
            var conexaoTask = client.ConnectAsync(ip, porta);
            var timeoutTask = Task.Delay(TimeoutMs);

            var completada = await Task.WhenAny(conexaoTask, timeoutTask);
            cronometro.Stop();

            if (completada == timeoutTask || !client.Connected)
            {
                return (false, cronometro.ElapsedMilliseconds, "Tempo limite de conexao excedido");
            }

            return (true, cronometro.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            cronometro.Stop();
            return (false, cronometro.ElapsedMilliseconds, ex.Message);
        }
    }
}
