namespace HollowWake.Client.UI.Logic;

// The rewardless crossing from arpg-game-world@64819bd, with all tuning in presentation content.
public sealed record SpiritOpening(double Lane, double Width);
public sealed class SpiritGate(int id, double u, List<SpiritOpening> openings)
{
    public int Id = id;
    public double U = u;
    public List<SpiritOpening> Openings = openings;
    public bool Resolved, Hit;
}
public sealed class SpiritPickup(int id, int choice, double u, double lane, string kind)
{
    public int Id = id, Choice = choice;
    public double U = u, Lane = lane, Fade;
    public string Kind = kind, State = "live";
}
public sealed class SpiritCurrent(int id, int gate, double u, double lane, string placement)
{
    public int Id = id, Gate = gate;
    public double U = u, Lane = lane, Fade;
    public string Placement = placement;
    public bool Taken;
}
public sealed class SpiritBurst(double u, double lane, string kind)
{
    public double U = u, Lane = lane, Age;
    public string Kind = kind;
}

public sealed class SpiritRun
{
    public readonly LoadingSpec Spec;
    public readonly string Direction;
    private readonly Func<double> _random;
    public double Time, Lane, Surge, Dash, Hinder, Distance;
    public int Streak, Passed, Hits, Collected, BoostGates, CurrentsTaken;
    public List<SpiritGate> Gates = new();
    public List<SpiritPickup> Pickups = new();
    public List<SpiritCurrent> Currents = new();
    public List<SpiritBurst> Bursts = new();
    private int _nextId = 1;
    private double _untilGate;
    private List<SpiritOpening> _previous;
    private SpiritRules C => Spec.Rules;

    public SpiritRun(LoadingSpec spec, string direction, uint seed)
    {
        Spec = spec;
        Direction = direction;
        _random = new LoadingRng(seed).Next;
        _previous = new() { new(0, C.GateGapMax) };
        Spawn();
    }
    public double Speed => Math.Min(C.MaxBoostSpeed, Math.Min(C.MaxSpeed, 1 + (Streak + BoostGates) * C.SpeedPerGate)
        + C.CurrentExtra * Math.Min(1, Dash / C.CurrentEase));
    public double PlayerU => C.PlayerU * Math.Min(1, Time / C.EntrySeconds);
    private T Pick<T>(IReadOnlyList<T> values) => values[Math.Min(values.Count - 1, (int)Math.Floor(_random() * values.Count))];
    private static SpiritOpening Nearest(IEnumerable<SpiritOpening> openings, double lane) => openings.MinBy(o => Math.Abs(o.Lane - lane))!;

    public IEnumerable<(double Low, double High)> Solids(SpiritGate gate)
    {
        var low = -C.HalfWidth;
        foreach (var opening in gate.Openings)
        {
            yield return (low, opening.Lane - opening.Width / 2);
            low = opening.Lane + opening.Width / 2;
        }
        yield return (low, C.HalfWidth);
    }

    private void Spawn()
    {
        var c = C;
        var roll = _random();
        var profile = Spec.GateProfiles.First(p => roll < p.Until);
        var drift = (_random() * 2 - 1) * profile.Drift;
        var openings = profile.Lanes.Select(lane => new SpiritOpening(lane + drift, c.GateGapMin + _random() * (profile.Maximum - c.GateGapMin))).ToList();
        var id = _nextId++;
        var u = c.Length + c.SpawnAhead;
        Gates.Add(new(id, u, openings));
        var to = Pick(openings).Lane;
        var from = Nearest(_previous, to).Lane;
        var currentRoll = _random();
        void Current(double lane, double at, string placement) =>
            Currents.Add(new(id * 3 + Currents.Count(b => b.Gate == id), id, at, lane, placement));
        if (currentRoll < c.OpeningCurrentChance)
        {
            var first = Pick(openings);
            Current(first.Lane, u, "opening");
            if (openings.Count > 1 && _random() < c.SecondCurrentChance)
                Current(Pick(openings.Where(o => o != first).ToList()).Lane, u, "opening");
        }
        if (currentRoll >= c.OpeningCurrentChance && currentRoll < c.BetweenCurrentThreshold)
            Current((from + to) / 2, u - c.GateSpacing / 2, "between");
        else
        {
            var pair = Pick(Spec.Choices);
            for (var i = 0; i < pair.Length; i++)
            {
                var along = c.GateSpacing / 2 + (i != 0 ? 1 : -1) * c.ChoiceStagger;
                double Reach(double distance) => (distance - c.Radius - c.GateThickness / 2 - c.RouteMargin) * c.SteerSpeed / (c.BaseSpeed * c.MaxBoostSpeed);
                var low = Math.Max(from - Reach(along), to - Reach(c.GateSpacing - along));
                var high = Math.Min(from + Reach(along), to + Reach(c.GateSpacing - along));
                var bend = (i != 0 ? -1 : 1) * (c.ChoiceBendMin + _random() * c.ChoiceBendRange);
                var lane = Math.Max(low, Math.Min(high, from + (to - from) * along / c.GateSpacing + bend));
                Pickups.Add(new(id * 2 + i, id, u - c.GateSpacing + along, lane, pair[i]));
            }
        }
        _previous = openings;
        _untilGate += c.GateSpacing;
    }

    private void Burst(string kind)
    {
        Bursts.Add(new(PlayerU, Lane, kind));
        if (Bursts.Count > C.MaxBursts) Bursts.RemoveAt(0);
    }

    public void Step(double seconds, double axis, double? target = null)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        var c = C;
        var remaining = Math.Min(seconds, c.MaxFrame);
        while (remaining > 0)
        {
            var dt = Math.Min(remaining, c.Step);
            remaining -= dt;
            Time += dt;
            Hinder = Math.Max(0, Hinder - dt);
            Surge = Math.Max(0, Surge - dt);
            Dash = Math.Max(0, Dash - dt);
            var steer = c.SteerSpeed * dt * (Hinder > 0 ? c.HinderedSteer : 1);
            var input = double.IsFinite(axis) ? Math.Clamp(axis, -1, 1) : 0;
            var aim = target.HasValue && double.IsFinite(target.Value) ? target : null;
            Lane = Math.Clamp(Lane + (Math.Abs(input) > 0.01 ? input * steer
                : aim is null ? 0 : Math.Clamp(aim.Value - Lane, -steer, steer)), -c.HalfWidth + c.Radius, c.HalfWidth - c.Radius);
            var travel = c.BaseSpeed * Speed * (Hinder > 0 ? c.HinderSpeed : 1) * dt;
            Distance += travel;
            _untilGate -= travel;
            foreach (var burst in Bursts) { burst.Age += dt; burst.U -= travel * c.BurstDrift; }
            Bursts.RemoveAll(b => b.Age >= c.BurstSeconds);
            if (_untilGate <= 0) Spawn();
            foreach (var gate in Gates)
            {
                gate.U -= travel;
                if (gate.Resolved) continue;
                var overlap = Math.Abs(gate.U - PlayerU) <= c.GateThickness / 2 + c.Radius;
                var clear = gate.Openings.Any(o => Math.Abs(Lane - o.Lane) + c.Radius < o.Width / 2);
                if (overlap && !clear)
                {
                    gate.Resolved = gate.Hit = true;
                    Hits++;
                    Streak = BoostGates = 0;
                    Surge = Dash = 0;
                    Hinder = c.HinderSeconds;
                    Burst("impact");
                }
                else if (gate.U + c.GateThickness / 2 < PlayerU - c.Radius)
                {
                    gate.Resolved = true; Passed++; Streak++; Burst("gate");
                }
            }
            foreach (var pickup in Pickups)
            {
                pickup.U -= travel;
                if (pickup.State != "live") { pickup.Fade += dt; continue; }
                if (Hinder > 0 || Math.Sqrt(Math.Pow(pickup.U - PlayerU, 2) + Math.Pow(pickup.Lane - Lane, 2)) > c.Radius + c.PickupRadius) continue;
                var def = Spec.Pickups[pickup.Kind];
                Collected++; BoostGates += def.BoostGates;
                if (def.BoostGates != 0) Surge = c.SurgeSeconds;
                Burst(pickup.Kind);
                foreach (var sibling in Pickups.Where(p => p.Choice == pickup.Choice))
                {
                    sibling.State = sibling == pickup ? "taken" : "released";
                    sibling.Fade = 0;
                }
            }
            foreach (var current in Currents)
            {
                current.U -= travel;
                if (current.Taken) { current.Fade += dt; continue; }
                if (Hinder > 0 || Math.Abs(current.U - PlayerU) > c.CurrentHalfLength + c.Radius
                    || Math.Abs(current.Lane - Lane) > c.CurrentHalfWidth + c.Radius) continue;
                current.Taken = true; CurrentsTaken++; Dash = c.CurrentSeconds; Burst("current");
            }
            Gates.RemoveAll(g => g.U <= -c.DespawnBehind);
            Pickups.RemoveAll(p => p.U <= -c.DespawnBehind || (p.State != "live" && p.Fade >= c.PickupFade));
            Currents.RemoveAll(p => p.U <= -c.DespawnBehind || (p.Taken && p.Fade >= c.PickupFade));
        }
    }
}

/// <summary>One collision, drawing and pointing transform, including narrow and tall screens.</summary>
public sealed class SpiritLayout(double width, double height, string direction, SpiritRules c)
{
    private readonly bool _vertical = direction == "down";
    public double Scale { get; } = Math.Max(0.000001, Math.Min((direction == "down" ? height : width) / c.Length,
        (direction == "down" ? width : height) / (c.HalfWidth * 2)));
    private double X => (width - (_vertical ? c.HalfWidth * 2 : c.Length) * Scale) / 2;
    private double Y => (height - (_vertical ? c.Length : c.HalfWidth * 2) * Scale) / 2;
    public (double X, double Y) Point(double u, double v) => _vertical
        ? (X + (v + c.HalfWidth) * Scale, Y + u * Scale)
        : (X + (direction == "right" ? u : c.Length - u) * Scale, Y + (v + c.HalfWidth) * Scale);
    public double Lane(double x, double y) => (_vertical ? x - X : y - Y) / Scale - c.HalfWidth;
}
