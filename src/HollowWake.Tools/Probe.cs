using System.Net.Sockets;
using HollowWake.Protocol;

namespace HollowWake.Tools;

/// <summary>
/// Connects to a running server over TCP, logs in, creates a character, enters, moves, and reports what came back.
/// Hello features can be asked for (<c>entity_delta</c>, <c>interface</c>): with deltas the probe follows itself
/// through them, and with the interface it checks that the inventory, weave slot, passive and page states arrive.
/// </summary>
public static class Probe
{
    public static async Task<int> RunAsync(string host, int port, string account, double seconds, IReadOnlyList<string>? features = null)
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
        Send(new HelloMsg("probe", ProtocolInfo.Version) { Features = features is { Count: > 0 } ? features.ToList() : null });
        Send(new LoginMsg(account, ""));
        await PumpAsync(1.0);
        var welcome = inbox.OfType<WelcomeMsg>().LastOrDefault();
        var granted = welcome?.Features ?? new List<string>();
        if (features is { Count: > 0 })
        {
            Console.WriteLine($"asked for features: {string.Join(", ", features)}; granted: {(granted.Count == 0 ? "none" : string.Join(", ", granted))}");
        }
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
        var day = enter.TimeOfDay is { } fraction ? $"; day of {enter.DayLength:0} s at {fraction:0.000}" : "; no day on the wire";
        Console.WriteLine($"entered zone {enter.Zone} at ({enter.X:0.0}, {enter.Y:0.0}, {enter.Z:0.0}); seed {enter.World.Seed}; tick {enter.TickRate}{day}");
        Send(new ChatMsg("zone", "probe says hello"));
        // A saved character may stand where one way is blocked, so walk until something moves.
        var directions = new (float X, float Y, string Name)[] { (0, -1, "south"), (1, 0, "east"), (0, 1, "north"), (-1, 0, "west") };
        var walked = "nowhere";
        for (var i = 0; i < directions.Length; i++)
        {
            Send(new MoveDirMsg(directions[i].X, directions[i].Y));
            await PumpAsync(i == 0 ? seconds / 2 : seconds / 4);
            if (Displacement(inbox, enter) > 1)
            {
                walked = directions[i].Name;
                break;
            }
        }
        Send(new JumpMsg());
        Send(new StopMsg());
        await PumpAsync(seconds / 2);
        var self = SelfStates(inbox, enter.EntityKey);
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
        var ok = true;
        if (granted.Contains(ProtocolInfo.InterfaceFeature))
        {
            var inventory = inbox.OfType<InventoryStateMsg>().LastOrDefault();
            var slots = inbox.OfType<WeaveSlotsMsg>().LastOrDefault();
            var passives = inbox.OfType<PassiveStateMsg>().LastOrDefault();
            var pages = inbox.OfType<PageStateMsg>().LastOrDefault();
            Console.WriteLine($"interface: bag {inventory?.Bag.Width}x{inventory?.Bag.Height} with {inventory?.Bag.Items.Count} items, {inventory?.Equipment.Count} worn; " +
                $"{slots?.Seats.Count(s => s.Key is not null)} of {slots?.Seats.Count} weave seats filled; {passives?.Allocated.Count} passives, points {string.Join(", ", passives?.Points.Select(p => p.Key + " " + p.Value) ?? Array.Empty<string>())}; " +
                $"pages {string.Join(", ", pages?.Pages.Select(p => p.Id + (p.Sealed ? " (sealed)" : "") + (p.Pips > 0 ? $" ({p.Pips})" : "")) ?? Array.Empty<string>())}");
            if (inventory is null || slots is null || passives is null || pages is null)
            {
                Console.WriteLine("PROBE: interface granted but its states did not all arrive");
                ok = false;
            }
        }
        var moved = Displacement(inbox, enter) > 1;
        if (!moved)
        {
            Console.WriteLine("PROBE: did not move in any direction");
            return 2;
        }
        Console.WriteLine(ok ? $"PROBE OK (walked {walked})" : "PROBE FAILED");
        return ok ? 0 : 3;
    }

    /// <summary>The probe's own states as it was told them: full states from spawns and updates, and deltas applied to the last one.</summary>
    private static List<EntityState> SelfStates(List<Message> inbox, long key)
    {
        var states = new List<EntityState>();
        EntityState? last = null;
        foreach (var m in inbox)
        {
            switch (m)
            {
                case EntitySpawnMsg s when s.State.Key == key:
                    last = s.State;
                    break;
                case EntityUpdateMsg u:
                    foreach (var s in u.States)
                    {
                        if (s.Key == key)
                        {
                            last = s;
                            states.Add(s);
                        }
                    }
                    break;
                case EntityDeltaMsg d when last is not null:
                    foreach (var delta in d.Deltas)
                    {
                        if (delta.Key == key)
                        {
                            last = delta.ApplyTo(last);
                            states.Add(last);
                        }
                    }
                    break;
            }
        }
        return states;
    }

    /// <summary>How far the probe has been seen from where it entered, on the larger horizontal axis.</summary>
    private static double Displacement(List<Message> inbox, EnterWorldMsg enter)
    {
        var last = SelfStates(inbox, enter.EntityKey).LastOrDefault();
        return last is null ? 0 : Math.Max(Math.Abs(last.X - enter.X), Math.Abs(last.Y - enter.Y));
    }
}
