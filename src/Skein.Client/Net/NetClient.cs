using System.Collections.Concurrent;
using System.Net.Sockets;
using Skein.Protocol;

namespace Skein.Client.Net;

/// <summary>
/// TCP client for the Skein wire protocol. Reads on a background task, hands decoded messages to the
/// main thread through a queue, and sends from the main thread. Mirrors the server's TcpTransport.
/// </summary>
public sealed class NetClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<Message> _inbox = new();
    private readonly object _sendLock = new();

    public bool IsConnected => _client?.Connected ?? false;
    public string? LastError { get; private set; }
    public event Action<string>? Closed;
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
            _ = Task.Run(() => ReadLoop(_cts.Token));
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

    private async Task ReadLoop(CancellationToken ct)
    {
        var reader = new MessageSerializer.FrameReader();
        var buffer = new byte[32 * 1024];
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                var n = await _stream.ReadAsync(buffer, ct);
                if (n <= 0)
                {
                    Closed?.Invoke("closed by server");
                    return;
                }
                BytesIn += n;
                reader.Append(buffer.AsSpan(0, n));
                while (reader.TryRead(out var payload))
                {
                    var m = MessageSerializer.Decode(payload);
                    if (m is not null)
                    {
                        _inbox.Enqueue(m);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Closed?.Invoke(ex.Message);
        }
    }

    public void Send(Message message)
    {
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
            Closed?.Invoke(ex.Message);
        }
    }

    public bool TryDequeue(out Message message) => _inbox.TryDequeue(out message!);

    public void Disconnect(string reason)
    {
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
