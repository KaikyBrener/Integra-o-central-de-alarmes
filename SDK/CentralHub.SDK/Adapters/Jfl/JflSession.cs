using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Adapters.Jfl;

/// <summary>
/// Uma conexao TCP aberta por uma central JFL (a central disca para o servidor). Vive do accept ate o
/// fechamento. Antes do quadro 0x21 a sessao ainda nao esta identificada (<see cref="Info"/> nulo).
/// </summary>
public sealed class JflSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly JflSequence _sequence = new();
    private readonly ConcurrentDictionary<(byte Cmd, byte Seq), TaskCompletionSource<JflStatus>> _pending = new();
    private int _closed;

    /// <summary>Quando definido, cada quadro enviado/recebido e registrado em hexadecimal (depuracao de protocolo).</summary>
    internal Microsoft.Extensions.Logging.ILogger? RawLogger { get; set; }

    /// <summary>Registro de protocolo (RX/TX, erros, timeouts) consultado pela tela de Logs.</summary>
    internal JflProtocolLog? ProtocolLog { get; set; }

    internal JflSession(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        RemoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "?";
        ConnectedAtUtc = DateTime.UtcNow;
        LastFrameAtUtc = ConnectedAtUtc;
    }

    public string RemoteEndPoint { get; }
    public DateTime ConnectedAtUtc { get; }
    public DateTime LastFrameAtUtc { get; internal set; }

    public JflConnectionInfo? Info { get; internal set; }
    public JflModelProfile? Profile { get; internal set; }
    public string? Serial => Info?.Serial;

    public JflStatus? LastStatus { get; private set; }
    public DateTime? LastStatusAtUtc { get; private set; }

    public bool IsConnected => Volatile.Read(ref _closed) == 0 && _client.Connected;

    internal NetworkStream Stream => _stream;

    internal async Task WriteAsync(byte[] frame, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            RawLogger?.LogInformation("tx {Serial} {Remote} {Frame}", Serial ?? "?", RemoteEndPoint, Convert.ToHexString(frame));
            if (ProtocolLog is not null && JflFrame.IsValid(frame))
            {
                ProtocolLog.Frame(this, new JflFrame(frame), JflLogDirection.Tx);
            }
            await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Envia um comando e aguarda o quadro de status que a central devolve ecoando CMD e SEQ.
    /// Um comando por vez por central. Lanca <see cref="TimeoutException"/> se nao houver resposta e
    /// <see cref="IOException"/> se a conexao cair.
    /// </summary>
    public async Task<JflStatus> SendAsync(Func<byte, byte[]> builder, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!IsConnected) throw new IOException("A central nao esta conectada.");

        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var seq = _sequence.Next();
            var frame = builder(seq);
            var key = (Cmd: frame[3], Seq: frame[2]);
            var tcs = new TaskCompletionSource<JflStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[key] = tcs;
            try
            {
                await WriteAsync(frame, ct).ConfigureAwait(false);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                using var reg = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    var message = $"Sem resposta da central ao comando 0x{key.Cmd:X2} em {timeout.TotalSeconds:0.#}s.";
                    ProtocolLog?.Add(Serial, RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Timeout, message);
                    throw new TimeoutException(message);
                }
            }
            finally
            {
                _pending.TryRemove(key, out _);
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    /// <summary>Atualiza o ultimo status e resolve o comando pendente correspondente, se houver.</summary>
    internal void OnStatus(JflStatus status, byte cmd, byte seq)
    {
        LastStatus = status;
        LastStatusAtUtc = DateTime.UtcNow;
        if (_pending.TryGetValue((cmd, seq), out var tcs))
        {
            tcs.TrySetResult(status);
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;

        foreach (var tcs in _pending.Values)
        {
            tcs.TrySetException(new IOException("Conexao com a central encerrada."));
        }

        try { _client.Close(); } catch { /* ja fechado */ }
    }

    public void Dispose()
    {
        Close();
        _writeLock.Dispose();
        _commandLock.Dispose();
    }
}

/// <summary>
/// Sessoes JFL identificadas, indexadas pelo numero de serie (nunca pelo IP: a central pode voltar com
/// outro IP por DHCP e continua sendo a mesma). Compartilhada entre o servidor de escuta e o
/// <see cref="CentralHub.SDK.Adapters.JflAdapter"/>.
/// </summary>
public sealed class JflSessionRegistry
{
    public static JflSessionRegistry Default { get; } = new();

    /// <summary>Registro de protocolo compartilhado por todas as sessoes deste registro.</summary>
    public JflProtocolLog Log { get; } = new();

    private readonly ConcurrentDictionary<string, JflSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Porta em que o servidor de escuta esta aceitando centrais (informativo, para mensagens).</summary>
    public int? ListenPort { get; internal set; }

    public IReadOnlyCollection<JflSession> Sessions => _sessions.Values.ToList();

    public JflSession? Find(string? serial) =>
        !string.IsNullOrWhiteSpace(serial) && _sessions.TryGetValue(serial.Trim(), out var s) && s.IsConnected ? s : null;

    /// <summary>Registra a sessao. Se ja havia outra para o mesmo serial (a central reconectou), a antiga e encerrada.</summary>
    internal JflSession? Bind(JflSession session)
    {
        JflSession? previous = null;
        _sessions.AddOrUpdate(
            session.Serial!,
            session,
            (_, old) => { previous = old; return session; });
        if (previous is not null && !ReferenceEquals(previous, session))
        {
            previous.Close();
            return previous;
        }
        return null;
    }

    internal void Unbind(JflSession session)
    {
        if (session.Serial is { } serial)
        {
            // So remove se ainda for esta sessao (uma reconexao pode ja ter assumido o lugar).
            _sessions.TryRemove(new KeyValuePair<string, JflSession>(serial, session));
        }
    }
}
