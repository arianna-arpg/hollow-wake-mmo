using System.Collections.Concurrent;
using System.Net.Sockets;
using HollowWake.Protocol;

namespace HollowWake.Client.Net;

/// <summary>
/// TCP client for the Hollow Wake MMO wire protocol. Reads on a background task, hands decoded messages to the
/// main thread through a queue, and sends from the main thread. Mirrors the server's TcpTransport.
/// </summary>
public sealed class NetClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<(long Generation, Message Message)> _inbox = new();
    private long _generation;
    public long Generation => Interlocked.Read(ref _generation);
    private readonly object _sendLock = new();

    public bool IsConnected => _client?.Connected ?? false;
    public string? LastError { get; private set; }
    public event Action<long, string>? Closed;
    public long BytesIn { get; private set; }
    public long BytesOut { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port)
    {
        Disconnect("reconnect");
        try
        {
            _client = new TcpClient { NoDelay = true };
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();
            var stream = _stream;
            var token = _cts.Token;
            var generation = Generation;
            _ = Task.Run(() => ReadLoop(stream, token, generation));
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _client = null;
            return false;
        }
    }

    private async Task ReadLoop(NetworkStream stream, CancellationToken ct, long generation)
    {
        var reader = new MessageSerializer.FrameReader();
        var buffer = new byte[32 * 1024];
        try
        {
            while (!ct.IsCancellationRequested && _stream == stream)
            {
                var n = await stream.ReadAsync(buffer, ct);
                if (n <= 0)
                {
                    if (_stream == stream && !ct.IsCancellationRequested) Closed?.Invoke(generation, "closed by server");
                    return;
                }
                BytesIn += n;
                reader.Append(buffer.AsSpan(0, n));
                while (reader.TryRead(out var payload))
                {
                    var m = MessageSerializer.Decode(payload);
                    if (m is not null && _stream == stream && !ct.IsCancellationRequested)
                    {
                        _inbox.Enqueue((generation, m));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_stream == stream && !ct.IsCancellationRequested)
            {
                LastError = ex.Message;
                Closed?.Invoke(generation, ex.Message);
            }
        }
    }

    public void Send(Message message)
    {
        var generation = Generation;
        if (_stream is null)
        {
            return;
        }
        var framed = MessageSerializer.Frame(MessageSerializer.Encode(message));
        try
        {
            lock (_sendLock)
            {
                _stream.Write(framed);
            }
            BytesOut += framed.Length;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Closed?.Invoke(generation, ex.Message);
        }
    }

    public bool TryDequeue(out Message message)
    {
        while (_inbox.TryDequeue(out var row))
            if (row.Generation == Generation) { message = row.Message; return true; }
        message = null!; return false;
    }

    public void Disconnect(string reason)
    {
        Interlocked.Increment(ref _generation);
        _cts?.Cancel();
        try
        {
            _client?.Close();
        }
        catch
        {
            // closing
        }
        _client = null;
        _stream = null;
        _ = reason;
    }

    public void Dispose() => Disconnect("dispose");
}
