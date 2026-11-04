using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Adapters.Jfl;

public sealed class JflServerOptions
{
    /// <summary>
    /// Porta de escuta. A central guarda a porta de destino com 4 digitos (enderecos 706/707 do manual),
    /// logo o valor deve ficar entre 1 e 9999. 9090 e o padrao documentado para os modulos JFL M-300 ETH.
    /// </summary>
    public int Port { get; set; } = 9090;

    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>Intervalo de keep-alive devolvido a central (1 a 20 minutos). Deve ser coerente com o programado.</summary>
    public int KeepAliveMinutes { get; set; } = JflEncoder.DefaultKeepAliveMinutes;

    /// <summary>Menor tolerancia de silencio antes de derrubar a conexao (a central rediscara sozinha).</summary>
    public TimeSpan IdleFloor { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>Quando false, centrais cujo serial nao passa em <see cref="AcceptPanel"/> sao recusadas (RESULT 0x00).</summary>
    public bool AcceptUnknownPanels { get; set; } = true;

    /// <summary>Filtro opcional de centrais permitidas (por serial). Ignorado se <see cref="AcceptUnknownPanels"/> = true.</summary>
    public Func<string, bool>? AcceptPanel { get; set; }

    /// <summary>Registra em log (nivel Information) todos os quadros trocados, em hexadecimal. Para validar modelos/firmwares.</summary>
    public bool LogRawFrames { get; set; }

    internal TimeSpan IdleTimeout =>
        TimeSpan.FromSeconds(Math.Max(IdleFloor.TotalSeconds, 2.5 * KeepAliveMinutes * 60));
}

/// <summary>
/// Servidor TCP que faz o papel do "Active NET": aceita as conexoes iniciadas pelas centrais JFL,
/// confirma conexao/keep-alive/eventos, mantem o estado e permite enviar comandos pela conexao ativa.
///
/// Este e o canal de MONITORAMENTO (central -> IP/porta programados nos enderecos 702/703 e 706/707).
/// Nao tem relacao com a nuvem/aplicativo JFL (MQTT TLS na porta 8883).
/// </summary>
public sealed class JflServer : IEventoListener, IAsyncDisposable
{
    private readonly JflServerOptions _options;
    private readonly ILogger _logger;
    private readonly List<Task> _connections = new();
    private readonly object _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public JflServer(JflServerOptions? options = null, JflSessionRegistry? registry = null, ILogger? logger = null)
    {
        _options = options ?? new JflServerOptions();
        Registry = registry ?? JflSessionRegistry.Default;
        _logger = logger ?? NullLogger.Instance;
    }

    public JflSessionRegistry Registry { get; }

    /// <summary>Porta efetivamente em uso (util quando Port = 0 nos testes).</summary>
    public int BoundPort { get; private set; }

    public event Action<EventoCentral>? EventoRecebido;
    public event Action<JflSession>? PanelConnected;
    public event Action<JflSession>? PanelDisconnected;

    public async Task IniciarEscutaAsync(CancellationToken cancellationToken)
    {
        Start(cancellationToken);
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // encerramento normal
        }
        finally
        {
            await StopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Inicia a escuta e retorna imediatamente.</summary>
    public void Start(CancellationToken cancellationToken = default)
    {
        if (_listener is not null) throw new InvalidOperationException("Servidor JFL ja iniciado.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Parse(_options.BindAddress), _options.Port);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Registry.ListenPort = BoundPort;
        _logger.LogInformation("Servidor JFL escutando em {Address}:{Port}", _options.BindAddress, BoundPort);

        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ja parado */ }

        foreach (var session in Registry.Sessions) session.Close();

        Task[] pending;
        lock (_gate) pending = _connections.ToArray();
        try { await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); } catch { /* melhor esforco */ }

        _listener = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException or InvalidOperationException)
            {
                if (ct.IsCancellationRequested) return;
                _logger.LogWarning(ex, "Falha ao aceitar conexao JFL");
                continue;
            }

            client.NoDelay = true;
            var task = Task.Run(() => HandleConnectionAsync(client, ct));
            lock (_gate)
            {
                _connections.RemoveAll(t => t.IsCompleted);
                _connections.Add(task);
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken serverToken)
    {
        using var session = new JflSession(client);
        if (_options.LogRawFrames) session.RawLogger = _logger;
        session.ProtocolLog = Registry.Log;
        Registry.Log.Add(null, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Conexao, "Conexao TCP aberta pela central");
        var reader = new JflFrameReader();
        var buffer = new byte[4096];
        var bound = false;

        _logger.LogDebug("Conexao JFL aberta de {Remote}", session.RemoteEndPoint);

        try
        {
            while (!serverToken.IsCancellationRequested)
            {
                int read;
                using (var idle = CancellationTokenSource.CreateLinkedTokenSource(serverToken))
                {
                    idle.CancelAfter(_options.IdleTimeout);
                    try
                    {
                        read = await session.Stream.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!serverToken.IsCancellationRequested)
                    {
                        // Silencio alem do tolerado: fechamos para a central rediscar.
                        Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Timeout,
                            $"Sem comunicacao por {_options.IdleTimeout.TotalSeconds:0}s (watchdog); conexao encerrada para a central rediscar");
                        _logger.LogInformation("Central {Serial} ({Remote}) sem comunicacao por {Timeout}s; encerrando conexao",
                            session.Serial ?? "?", session.RemoteEndPoint, _options.IdleTimeout.TotalSeconds);
                        break;
                    }
                }

                if (read == 0) break;
                session.LastFrameAtUtc = DateTime.UtcNow;

                foreach (var frame in reader.Feed(buffer.AsSpan(0, read)))
                {
                    if (_options.LogRawFrames)
                    {
                        _logger.LogInformation("rx {Serial} {Remote} {Frame}", session.Serial ?? "?", session.RemoteEndPoint, frame);
                    }
                    Registry.Log.Frame(session, frame, JflLogDirection.Rx);

                    bound |= await HandleFrameAsync(session, frame, serverToken).ConfigureAwait(false);
                }

                if (reader.UnsupportedGeneration != JflUnsupportedGeneration.None)
                {
                    Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Erro,
                        $"Geracao de protocolo JFL nao suportada ({reader.UnsupportedGeneration}); conexao encerrada");
                    _logger.LogWarning(
                        "Central em {Remote} usa geracao de protocolo JFL nao suportada ({Generation}); encerrando conexao",
                        session.RemoteEndPoint, reader.UnsupportedGeneration);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            _logger.LogDebug("Conexao JFL de {Remote} encerrada: {Message}", session.RemoteEndPoint, ex.Message);
        }
        catch (Exception ex)
        {
            Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Erro, $"Erro inesperado: {ex.Message}");
            _logger.LogError(ex, "Erro inesperado na conexao JFL de {Remote}", session.RemoteEndPoint);
        }
        finally
        {
            session.Close();
            if (reader.DroppedBytes > 0)
            {
                Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Erro,
                    $"{reader.DroppedBytes} byte(s) descartado(s) na ressincronizacao do fluxo (checksum/lixo)");
            }
            Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Desconexao, "Conexao encerrada");
            if (bound)
            {
                Registry.Unbind(session);
                _logger.LogInformation("Central JFL {Serial} desconectada", session.Serial);
                SafeInvoke(() => PanelDisconnected?.Invoke(session));
            }
        }
    }

    /// <summary>Retorna true quando este quadro fez a sessao ser registrada.</summary>
    private async Task<bool> HandleFrameAsync(JflSession session, JflFrame frame, CancellationToken ct)
    {
        switch (frame.Cmd)
        {
            case JflCmd.Event:
            {
                // Confirma ANTES de decodificar: um evento nao confirmado e retransmitido para sempre.
                var counter = frame.Length >= 22 ? frame.Slice(17, 21) : ReadOnlySpan<byte>.Empty;
                await session.WriteAsync(JflEncoder.EventAck(frame.Seq, counter), ct).ConfigureAwait(false);
                try
                {
                    var evt = JflDecoder.DecodeEvent(frame);
                    var evento = JflEventMapper.ToEvento(evt, session.Serial, frame.Raw);
                    _logger.LogDebug("Evento JFL {Serial} codigo {Code} particao {Partition} sujeito {Subject}",
                        session.Serial, evt.Code, evt.Partition, evt.Subject);
                    SafeInvoke(() => EventoRecebido?.Invoke(evento));
                }
                catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
                {
                    Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Erro, $"Evento nao decodificado (ja confirmado): {ex.Message}", frame.Raw);
                    _logger.LogWarning(ex, "Evento JFL nao decodificado ({Frame}); ja confirmado", frame);
                }
                return false;
            }

            case JflCmd.KeepAlive:
                await session.WriteAsync(JflEncoder.KeepAliveAck(frame.Seq, _options.KeepAliveMinutes), ct).ConfigureAwait(false);
                return false;

            case JflCmd.Connection:
                return await HandleConnectionFrameAsync(session, frame, ct).ConfigureAwait(false);

            default:
                if (JflCmd.RepliesWithStatus(frame.Cmd) && frame.Length >= JflDecoder.StatusMinLength)
                {
                    try
                    {
                        session.OnStatus(JflDecoder.DecodeStatus(frame), frame.Cmd, frame.Seq);
                    }
                    catch (ArgumentException ex)
                    {
                        Registry.Log.Add(session.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Erro, $"Status invalido: {ex.Message}", frame.Raw);
                        _logger.LogWarning(ex, "Status JFL invalido ({Length} bytes)", frame.Length);
                    }
                }
                else
                {
                    _logger.LogDebug("Quadro JFL nao tratado: cmd 0x{Cmd:X2} seq 0x{Seq:X2} ({Length} bytes)", frame.Cmd, frame.Seq, frame.Length);
                }
                return false;
        }
    }

    private async Task<bool> HandleConnectionFrameAsync(JflSession session, JflFrame frame, CancellationToken ct)
    {
        if (frame.Length < JflDecoder.ConnectionFrameLength)
        {
            _logger.LogWarning("Quadro de conexao JFL com {Length} bytes (esperado {Expected}); recusando", frame.Length, JflDecoder.ConnectionFrameLength);
            await session.WriteAsync(JflEncoder.ConnectionAck(frame.Seq, accept: false, _options.KeepAliveMinutes), ct).ConfigureAwait(false);
            session.Close();
            return false;
        }

        var info = JflDecoder.DecodeConnection(frame);
        var accepted = !string.IsNullOrEmpty(info.Serial)
                       && (_options.AcceptUnknownPanels || (_options.AcceptPanel?.Invoke(info.Serial) ?? false));

        await session.WriteAsync(JflEncoder.ConnectionAck(frame.Seq, accepted, _options.KeepAliveMinutes), ct).ConfigureAwait(false);

        if (!accepted)
        {
            Registry.Log.Add(info.Serial, session.RemoteEndPoint, JflLogDirection.Sistema, JflLogKind.Conexao, "Central recusada pela configuracao do servidor");
            _logger.LogInformation("Central JFL {Serial} recusada", info.Serial);
            session.Close();
            return false;
        }

        session.Info = info;
        session.Profile = JflModelProfile.Resolve(info.ModelByte);
        var replaced = Registry.Bind(session);
        Registry.Log.Add(info.Serial, session.RemoteEndPoint, JflLogDirection.Sistema,
            replaced is null ? JflLogKind.Conexao : JflLogKind.Reconexao,
            replaced is null
                ? $"Central identificada: {session.Profile.Description}, firmware {info.Firmware}"
                : $"Reconexao: nova conexao substituiu a anterior ({replaced.RemoteEndPoint})");
        _logger.LogInformation(
            "Central JFL conectada: serie {Serial}, modelo {Model}, firmware {Firmware}, origem {Remote}{Replaced}",
            info.Serial, session.Profile.Description, info.Firmware, session.RemoteEndPoint,
            replaced is null ? "" : " (substituiu conexao anterior)");

        SafeInvoke(() => PanelConnected?.Invoke(session));

        // A central nunca envia status sozinha: pede o primeiro para popular zonas/particoes/PGM.
        _ = Task.Run(async () =>
        {
            try
            {
                await session.SendAsync(JflEncoder.StatusRequest, TimeSpan.FromSeconds(8), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or OperationCanceledException or ObjectDisposedException)
            {
                _logger.LogDebug("Status inicial da central {Serial} nao obtido: {Message}", info.Serial, ex.Message);
            }
        }, ct);

        return true;
    }

    private void SafeInvoke(Action action)
    {
        try { action(); }
        catch (Exception ex) { _logger.LogError(ex, "Falha em tratador de evento do servidor JFL"); }
    }
}

/// <summary>Converte eventos JFL para o modelo neutro do CentralHub.</summary>
public static class JflEventMapper
{
    public static EventoCentral ToEvento(JflPanelEvent e, string? serial, byte[]? raw = null)
    {
        var entry = JflContactIdTable.Find(e.Code);
        var evento = new EventoCentral
        {
            DataHora = DateTime.Now,
            Tipo = entry?.Tipo ?? TipoEvento.Geral,
            Fabricante = "JFL",
            Serial = serial,
            Conta = e.Account,
            Codigo = e.Code,
            Restauracao = e.Qualifier == '3' && entry?.Tipo is not (TipoEvento.Arme),
            Bruto = raw is null ? null : Convert.ToHexString(raw)
        };

        var partition = TryInt(e.Partition);
        var subject = TryInt(e.Subject);

        if (entry?.PartitionIsPgm == true)
        {
            // Manual JFL 14.9.2: o numero da PGM vai no campo particao.
            evento.Pgm = partition;
        }
        else
        {
            evento.Particao = partition;
        }

        switch (entry?.Subject)
        {
            case JflSubjectKind.Zone: evento.Zona = subject; break;
            case JflSubjectKind.User: evento.Usuario = subject; break;
        }

        var description = entry?.Description ?? $"Evento Contact ID {e.Code} (nao catalogado)";
        var detail = new List<string> { description };
        if (evento.Zona is { } z) detail.Add($"zona {z}");
        if (evento.Usuario is { } u) detail.Add($"usuario {u}");
        if (evento.Pgm is { } p) detail.Add($"PGM {p}");
        if (evento.Particao is { } part && part > 0) detail.Add($"particao {part}");
        if (entry is null && !string.IsNullOrEmpty(e.Subject)) detail.Add($"campo {e.Subject}");
        evento.Detalhe = string.Join(", ", detail);

        return evento;
    }

    private static int? TryInt(string value) => int.TryParse(value, out var n) ? n : null;
}
