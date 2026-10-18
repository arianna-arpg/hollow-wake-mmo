// Read-only duel probe for the AI inventory. Loads the built core from a scratch copy, runs a seeded arena
// duel exactly as the fight command does, and reports what each AI tried, failed, held and where it stood.
// Usage (from the worktree): dotnet fsi <this> A B seed seconds [kitA|-] [kitB|-] [timelineStep]
#r "bin/HollowWake.Core.dll"
#r "bin/YamlDotNet.dll"
#r "bin/Microsoft.Extensions.Logging.Abstractions.dll"
#r "bin/Microsoft.Extensions.DependencyInjection.Abstractions.dll"

open System
open System.Collections.Generic
open System.Globalization
open System.Numerics
open HollowWake.Core
open HollowWake.Core.Content
open HollowWake.Core.Sim
open HollowWake.Core.Patterns
open HollowWake.Core.Definitions
open HollowWake.Core.Components
open HollowWake.Core.Maths
open HollowWake.Core.Weaves
open HollowWake.Core.Ecs

CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture

let argv = fsi.CommandLineArgs |> Array.skip 1
let a = argv.[0]
let b = argv.[1]
let seed = argv.[2]
let seconds = float argv.[3]
let kitOf i = if argv.Length > i && argv.[i] <> "-" then Some(argv.[i].Split(',')) else None
let kitA = kitOf 4
let kitB = kitOf 5
let step = if argv.Length > 6 then float argv.[6] else 0.5

let mutable report = Unchecked.defaultof<ValidationReport>
let registry = ContentRegistry.Load([| "content/core" |], &report)
let sim = ZoneBuilder.CreateArena(registry, Rng.Hash64(seed))
let factory = sim.Service<PatternFactory>()
let ea = factory.Spawn(sim, a, Vector3(10.0f, 10.0f, 0.0f), SpawnOptions(Faction = "red"))
let eb = factory.Spawn(sim, b, Vector3(14.0f, 10.0f, 0.0f), SpawnOptions(Faction = "blue"))
let fr = FactionDef(Id = "red")
fr.Relations.["blue"] <- Stance.Hostile
let fb = FactionDef(Id = "blue")
fb.Relations.["red"] <- Stance.Hostile
registry.With(fr).With(fb) |> ignore

let setKit (e: Entity) (kit: string[]) =
    let book = sim.World.GetOrAdd<WeaveBook>(e)
    book.Slots.Clear()
    for k in kit do
        book.Add(WeaveSpec(Preset = k)) |> ignore
    book.Changed()

kitA |> Option.iter (setKit ea)
kitB |> Option.iter (setKit eb)

let name (e: Entity) =
    match sim.World.Get<Identity>(e) with
    | null -> "?"
    | i -> i.ToString()

let tag (e: Entity) = if e = ea then "A" elif e = eb then "B" else "other"
let counts = SortedDictionary<string, int>(StringComparer.Ordinal)
let bump key =
    let mutable n = 0
    counts.TryGetValue(key, &n) |> ignore
    counts.[key] <- n + 1
let lines = List<string>()
let note (s: string) = lines.Add(sprintf "t=%.2f %s" sim.Clock.Time s)

sim.Events.Subscribe<WeaveFailed>(Action<WeaveFailed>(fun d -> bump (sprintf "%s failed %s: %s" (tag d.Caster) d.Weave d.Reason)))
sim.Events.Subscribe<WeaveStarted>(Action<WeaveStarted>(fun d -> bump (sprintf "%s started %s" (tag d.Caster) d.Weave)))
sim.Events.Subscribe<WeaveCast>(Action<WeaveCast>(fun d -> bump (sprintf "%s cast %s" (tag d.Caster) d.Weave)))
sim.Events.Subscribe<WeaveReleased>(Action<WeaveReleased>(fun d ->
    bump (sprintf "%s released %s (%s, %s)" (tag d.Caster) d.Weave d.Mode d.Reason)
    note (sprintf "%s released %s mode=%s reason=%s held=%.2f fill=%.2f stage=%d fired=%b" (tag d.Caster) d.Weave d.Mode d.Reason d.Held d.Fill d.Stage d.Fired)))
sim.Events.Subscribe<WeaveInterrupted>(Action<WeaveInterrupted>(fun d -> bump (sprintf "%s interrupted %s" (tag d.Caster) d.Weave)))
sim.Events.Subscribe<WeaveToggled>(Action<WeaveToggled>(fun d ->
    bump (sprintf "%s toggled %s on=%b (%s)" (tag d.Caster) d.Weave d.On d.Reason)
    note (sprintf "%s toggled %s on=%b reason=%s" (tag d.Caster) d.Weave d.On d.Reason)))
sim.Events.Subscribe<UsesChanged>(Action<UsesChanged>(fun d -> note (sprintf "%s uses %s now %.2f of %d" (tag d.Caster) d.Weave d.Uses d.Max)))
sim.Events.Subscribe<ChargeSpent>(Action<ChargeSpent>(fun d -> note (sprintf "%s spent %.0f %s (left %.0f)" (tag d.Holder) d.Amount d.Charge d.Total)))
sim.Events.Subscribe<MetaUsed>(Action<MetaUsed>(fun d -> bump (sprintf "%s meta %s %s" (tag d.Caster) d.Weave d.Meta)))
sim.Events.Subscribe<WeavePressed>(Action<WeavePressed>(fun d -> bump (sprintf "%s pressed %s" (tag d.Caster) d.Weave)))
sim.Events.Subscribe<GuardParried>(Action<GuardParried>(fun d -> bump (sprintf "%s parried with %s" (tag d.Bearer) d.Weave)))
sim.Events.Subscribe<GuardBroken>(Action<GuardBroken>(fun d -> bump (sprintf "%s guard broken %s" (tag d.Bearer) d.Weave)))
sim.Events.Subscribe<OverchargeStaged>(Action<OverchargeStaged>(fun d -> bump (sprintf "%s overcharge %s stage %d of %d" (tag d.Caster) d.Weave d.Stage d.Stages)))
sim.Events.Subscribe<DamageDealt>(Action<DamageDealt>(fun d -> if d.Total > 0.0 then bump (sprintf "%s dealt damage events" (tag d.Source))))
sim.Events.Subscribe<EntityDied>(Action<EntityDied>(fun d -> note (sprintf "%s died" (tag d.Entity))))
sim.Events.Subscribe<MinionSummoned>(Action<MinionSummoned>(fun d -> bump (sprintf "%s summoned (%s)" (tag d.Owner) (if isNull d.Weave then "" else d.Weave))))
sim.Events.Subscribe<DisplacementStarted>(Action<DisplacementStarted>(fun d -> bump (sprintf "%s displaced %s" (tag d.Entity) d.Kind)))

let ticks = int (Math.Ceiling(seconds * float sim.Clock.TicksPerSecond))
let lastPos = Dictionary<Entity, Vector3>()
let moved = Dictionary<Entity, float32>()
let stateTicks = SortedDictionary<string, int>(StringComparer.Ordinal)
let mutable nextLine = 0.0
let describe (e: Entity) =
    if not (sim.World.IsAlive(e)) || sim.World.Has<Dead>(e) then "dead"
    else
        let tr = sim.World.Get<Transform>(e)
        let bh = sim.World.Get<Behavior>(e)
        let book = sim.World.Get<WeaveBook>(e)
        let casting =
            match book with
            | null -> ""
            | bk when isNull bk.Casting -> ""
            | bk -> sprintf " casting=%s(%A)" bk.Casting.Slot.Key bk.Casting.Mode
        let st = match bh with | null -> "-" | x -> x.State
        sprintf "(%.2f,%.2f) %s%s" tr.Position.X tr.Position.Y st casting

for i in 1 .. ticks do
    sim.Tick()
    for e in [ ea; eb ] do
        if sim.World.IsAlive(e) && not (sim.World.Has<Dead>(e)) then
            let tr = sim.World.Get<Transform>(e)
            let mutable p = Vector3.Zero
            if lastPos.TryGetValue(e, &p) then
                let mutable m = 0.0f
                moved.TryGetValue(e, &m) |> ignore
                moved.[e] <- m + Vector3.Distance(p, tr.Position)
            lastPos.[e] <- tr.Position
            let bh = sim.World.Get<Behavior>(e)
            let book = sim.World.Get<WeaveBook>(e)
            let st = if isNull bh then "-" else bh.State
            let cast = if isNull book || isNull book.Casting then "free" else sprintf "casting %A" book.Casting.Mode
            let key = sprintf "%s state %s, %s" (tag e) st cast
            let mutable n = 0
            stateTicks.TryGetValue(key, &n) |> ignore
            stateTicks.[key] <- n + 1
    if sim.Clock.Time >= nextLine - 1e-9 then
        nextLine <- nextLine + step
        let ta = sim.World.Get<Transform>(ea)
        let tb = sim.World.Get<Transform>(eb)
        let dist = if isNull ta || isNull tb then -1.0 else float (ta.GroundDistanceTo(tb.Position))
        note (sprintf "A %s | B %s | dist %.2f" (describe ea) (describe eb) dist)

printfn "== %s (A) vs %s (B), seed %s, %.0f s%s%s" a b seed seconds (match kitA with Some k -> " kitA=" + String.Join(",", k) | None -> "") (match kitB with Some k -> " kitB=" + String.Join(",", k) | None -> "")
for kv in counts do printfn "  %5d  %s" kv.Value kv.Key
printfn "-- ticks by state (20 per second)"
for kv in stateTicks do printfn "  %5d  %s" kv.Value kv.Key
for e in [ ea; eb ] do
    let mutable m = 0.0f
    moved.TryGetValue(e, &m) |> ignore
    printfn "-- %s walked %.1f units" (tag e) m
let sa = sim.World.Get<Stats>(ea)
let sb = sim.World.Get<Stats>(eb)
let life (s: Stats) = if isNull s then "gone" else sprintf "%.0f/%.0f" (s.Resource("life")) (s.ResourceMax("life"))
printfn "-- life A %s  B %s" (life sa) (life sb)
printfn "-- timeline"
for l in lines do printfn "  %s" l
