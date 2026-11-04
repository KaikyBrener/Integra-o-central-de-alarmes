using System.Net.Sockets;

namespace CentralHub.SDK.Adapters;

/// <summary>
/// Leitura/escrita de frames ISECNet V2 sobre uma conexao TCP ja aberta
/// (via <see cref="TcpConnectionHelper.AbrirConexaoAsync"/>).
///
/// Frame: [LEN(1B)][DATA(LEN bytes)][CS(1B)] — estrutura confirmada em
/// IsecNetProtocol.cs. Esta classe SO sabe montar/receber essa forma de
/// frame a partir de bytes crus do socket; a validacao de checksum e o
/// parsing do conteudo de DATA continuam em IsecNetProtocol.cs.
///
/// Isto e I/O de protocolo (sabe a forma do frame), nao transporte generico
/// (TcpConnectionHelper cuida so de abrir/testar o socket, sem saber nada
/// de LEN/DATA/CS).
/// </summary>
internal static class IsecNetFrameIO
{
    private const int TimeoutMsPadrao = 3000;

    public static Task EnviarFrameAsync(NetworkStream stream, byte[] frame, CancellationToken ct)
        => stream.WriteAsync(frame, ct).AsTask();

    /// <summary>
    /// Le um frame [LEN][DATA][CS] do stream. Retorna null se o checksum for
    /// invalido, se a conexao cair, ou se estourar o timeout.
    ///
    /// TODO: nao ha confirmacao publica do tempo de resposta tipico da
    /// central real; TimeoutMsPadrao e um valor conservador — ajustar com
    /// testes em hardware real.
    /// TODO: nao ha confirmacao se a central sempre responde com exatamente
    /// um frame por requisicao, ou se pode fragmentar/enviar frames extras
    /// (ex.: eventos assincronos intercalados). Este leitor assume um frame
    /// por chamada.
    /// </summary>
    public static async Task<byte[]?> LerFrameAsync(NetworkStream stream, int timeoutMs = TimeoutMsPadrao)
    {
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            var lenBuffer = new byte[1];
            await LerExatoAsync(stream, lenBuffer, cts.Token);
            int len = lenBuffer[0];

            var restante = new byte[len + 1]; // DATA (len bytes) + CS (1 byte)
            await LerExatoAsync(stream, restante, cts.Token);

            var frame = new byte[1 + restante.Length];
            lenBuffer.CopyTo(frame, 0);
            restante.CopyTo(frame, 1);

            return IsecNetProtocol.ValidarChecksum(frame) ? frame : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task LerExatoAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int lido = 0;
        while (lido < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(lido, buffer.Length - lido), ct);
            if (n == 0)
            {
                throw new IOException("Conexao encerrada pela central antes do fim do frame.");
            }
            lido += n;
        }
    }
}
