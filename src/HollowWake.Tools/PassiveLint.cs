using HollowWake.Core.Content;
using HollowWake.Core.Definitions;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake passives</c> (row H11): lints the constellations. Links are undirected, as allocation reads them.
/// It reports links to nodes that do not exist and links that cross realms, orphans (nodes with no link at all),
/// nodes no anchor reaches (the callings' starts on the star, a realm's roots elsewhere; a free realm reaches all of
/// its nodes), and route costs: for each calling, the cheapest points from its start to every notable and keystone of
/// the star. Unreachable nodes, orphans and broken links fail the lint.
/// </summary>
public static class PassiveLint
{
    private const string Star = "star";

    public static int Run(ContentRegistry content, bool verbose)
    {
        var nodes = content.Each<PassiveNodeDef>().OrderBy(n => n.Id, StringComparer.Ordinal).ToDictionary(n => n.Id, StringComparer.Ordinal);
        var neighbors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var problems = 0;
        var warnings = 0;
        void Link(string a, string b)
        {
            if (!neighbors.TryGetValue(a, out var list))
            {
                neighbors[a] = list = new List<string>();
            }
            if (!list.Contains(b))
            {
                list.Add(b);
            }
        }
        foreach (var node in nodes.Values)
        {
            foreach (var link in node.Links)
            {
                if (!nodes.TryGetValue(link, out var other))
                {
                    Console.WriteLine($"error  {node.Id}: links to unknown node '{link}'");
                    problems++;
                    continue;
                }
                if (other.Realm != node.Realm)
                {
                    Console.WriteLine($"warn   {node.Id}: links across realms to '{link}' ({node.Realm} to {other.Realm})");
                    warnings++;
                }
                Link(node.Id, link);
                Link(link, node.Id);
            }
        }
        foreach (var node in nodes.Values)
        {
            if (!neighbors.ContainsKey(node.Id))
            {
                Console.WriteLine($"error  {node.Id}: an orphan, linked to nothing");
                problems++;
            }
        }

        // Reach: from every anchor of each realm.
        var realms = content.Each<PassiveRealmDef>().ToDictionary(r => r.Id, StringComparer.Ordinal);
        var anchors = new Dictionary<string, List<string>>(StringComparer.Ordinal) { [Star] = new() };
        foreach (var calling in content.Each<CallingDef>().OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (calling.StartNode is { } start)
            {
                if (nodes.ContainsKey(start))
                {
                    anchors[Star].Add(start);
                }
                else
                {
                    Console.WriteLine($"error  calling {calling.Id}: start node '{start}' does not exist");
                    problems++;
                }
            }
        }
        foreach (var realm in realms.Values)
        {
            anchors[realm.Id] = realm.Roots.Where(nodes.ContainsKey).ToList();
        }
        foreach (var group in nodes.Values.GroupBy(n => n.Realm).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (group.Key != Star && !realms.ContainsKey(group.Key))
            {
                Console.WriteLine($"error  realm '{group.Key}' is named by {group.Count()} node(s) but not declared");
                problems++;
                continue;
            }
            if (group.Key != Star && realms[group.Key].Adjacency == "free")
            {
                continue;
            }
            var reached = Walk(anchors.GetValueOrDefault(group.Key) ?? new List<string>(), neighbors, n => nodes[n].Realm == group.Key);
            foreach (var node in group)
            {
                if (!reached.Contains(node.Id))
                {
                    Console.WriteLine($"error  {node.Id}: no anchor of realm {group.Key} reaches it");
                    problems++;
                }
            }
        }

        // Route costs: the cheapest points from each calling's start to each notable and keystone of the star.
        var targets = nodes.Values.Where(n => n.Realm == Star && n.Kind is PassiveKind.Notable or PassiveKind.Keystone).ToList();
        foreach (var calling in content.Each<CallingDef>().OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (calling.StartNode is not { } start || !nodes.ContainsKey(start))
            {
                continue;
            }
            var cost = Cheapest(start, nodes, neighbors);
            var routes = targets.Where(t => cost.ContainsKey(t.Id)).Select(t => (t.Id, Cost: cost[t.Id])).OrderBy(r => r.Cost).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
            if (routes.Count == 0)
            {
                Console.WriteLine($"route  {calling.Id}: reaches no notable or keystone");
                continue;
            }
            Console.WriteLine($"route  {calling.Id}: {routes.Count} notables and keystones; nearest {routes[0].Id} at {routes[0].Cost}, farthest {routes[^1].Id} at {routes[^1].Cost}, mean {routes.Average(r => r.Cost):0.#}");
            if (verbose)
            {
                foreach (var (id, c) in routes)
                {
                    Console.WriteLine($"         {id,-28} {c}");
                }
            }
        }
        Console.WriteLine($"{nodes.Count} nodes in {anchors.Count} realms: {problems} problem(s), {warnings} warning(s)");
        return problems > 0 ? 1 : 0;
    }

    private static HashSet<string> Walk(IEnumerable<string> from, Dictionary<string, List<string>> neighbors, Func<string, bool> inside)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var open = new Queue<string>();
        foreach (var anchor in from)
        {
            if (seen.Add(anchor))
            {
                open.Enqueue(anchor);
            }
        }
        while (open.Count > 0)
        {
            var at = open.Dequeue();
            if (!neighbors.TryGetValue(at, out var next))
            {
                continue;
            }
            foreach (var n in next)
            {
                if (inside(n) && seen.Add(n))
                {
                    open.Enqueue(n);
                }
            }
        }
        return seen;
    }

    /// <summary>The cheapest points to own each node from a start: the sum of the costs of the nodes on the way, the start free.</summary>
    private static Dictionary<string, int> Cheapest(string start, Dictionary<string, PassiveNodeDef> nodes, Dictionary<string, List<string>> neighbors)
    {
        var best = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
        var open = new PriorityQueue<string, int>();
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var at, out var cost))
        {
            if (cost > best[at] || !neighbors.TryGetValue(at, out var next))
            {
                continue;
            }
            foreach (var n in next)
            {
                if (nodes[n].Realm != Star)
                {
                    continue;
                }
                var total = cost + Math.Max(0, nodes[n].Cost);
                if (!best.TryGetValue(n, out var known) || total < known)
                {
                    best[n] = total;
                    open.Enqueue(n, total);
                }
            }
        }
        return best;
    }
}
