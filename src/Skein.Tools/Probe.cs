using System.Net.Sockets;
using Skein.Protocol;

namespace Skein.Tools;

/// <summary>Connects to a running server over TCP, logs in, creates a character, enters, moves, and reports what came back.</summary>
public static class Probe
{
    public static async Task<int> RunAsync(string host, int port, string account, double seconds)
    {
        using var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"connect failed: {ex.Message}");
            return 1;
        }
        var stream = client.GetStream();
        var reader = new MessageSerializer.FrameReader();
        var counts = new Dictionary<string, int>();
        var inbox = new List<Message>();
        void Send(Message m) => stream.Write(MessageSerializer.Frame(MessageSerializer.Encode(m)));
        async Task PumpAsync(double wait)
        {
            var until = DateTime.UtcNow.AddSeconds(wait);
            var buffer = new byte[64 * 1024];
            while (DateTime.UtcNow < until)
            {
                if (!stream.DataAvailable)
                {
                    await Task.Delay(20);
                    continue;
                }
                var n = await stream.ReadAsync(buffer);
                if (n <= 0)
                {
                    break;
                }
                reader.Append(buffer.AsSpan(0, n));
                while (reader.TryRead(out var payload))
                {
                    var m = MessageSerializer.Decode(payload);
                    if (m is null)
                    {
                        continue;
                    }
                    inbox.Add(m);
                    var key = m.GetType().Name;
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }
        }
        Send(new HelloMsg("probe", ProtocolInfo.Version));
        Send(new LoginMsg(account, ""));
        await PumpAsync(1.0);
        var list = inbox.OfType<CharacterListMsg>().LastOrDefault();
        if (list is null)
        {
            Console.WriteLine("no character list; got " + string.Join(", ", counts.Keys));
            return 1;
        }
        string characterId;
        if (list.Characters.Count == 0)
        {
            Send(new CreateCharacterMsg("Probe", list.Callings.FirstOrDefault() ?? "holdfast"));
            await PumpAsync(1.0);
            characterId = inbox.OfType<CharacterListMsg>().Last().Characters[0].Id;
        }
        else
        {
            characterId = list.Characters[0].Id;
        }
        Send(new SelectCharacterMsg(characterId));
        await PumpAsync(1.5);
        var enter = inbox.OfType<EnterWorldMsg>().LastOrDefault();
        if (enter is null)
        {
            Console.WriteLine("did not enter the world; got " + string.Join(", ", counts.Select(kv => kv.Key + "=" + kv.Value)));
            return 1;
        }
        Console.WriteLine($"entered zone {enter.Zone} at ({enter.X:0.0}, {enter.Y:0.0}, {enter.Z:0.0}); seed {enter.World.Seed}; tick {enter.TickRate}");
        Send(new MoveDirMsg(0, -1));
        Send(new ChatMsg("zone", "probe says hello"));
        await PumpAsync(seconds / 2);
        Send(new JumpMsg());
        Send(new StopMsg());
        await PumpAsync(seconds / 2);
        var self = inbox.OfType<EntityUpdateMsg>().SelectMany(u => u.States).Where(s => s.Key == enter.EntityKey).ToList();
        var spawns = inbox.OfType<EntitySpawnMsg>().Select(s => s.State).ToList();
        Console.WriteLine($"messages: {string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "=" + kv.Value))}");
        Console.WriteLine($"self updates: {self.Count}; last position ({self.LastOrDefault()?.X:0.0}, {self.LastOrDefault()?.Y:0.0}, {self.LastOrDefault()?.Z:0.0})");
        Console.WriteLine($"entities seen: {spawns.Count} ({string.Join(", ", spawns.Select(s => s.Name).Distinct().Take(12))})");
        foreach (var n in inbox.OfType<NoticeMsg>().Take(5))
        {
            Console.WriteLine($"notice: {n.Text}");
        }
        foreach (var c in inbox.OfType<ChatBroadcastMsg>().Take(3))
        {
            Console.WriteLine($"chat: {c.From}: {c.Text}");
        }
        var moved = self.Count > 0 && Math.Abs(self.Last().Y - enter.Y) > 1;
        Console.WriteLine(moved ? "PROBE OK" : "PROBE: did not move");
        return moved ? 0 : 2;
    }
}
