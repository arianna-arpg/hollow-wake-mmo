# What a player wears, and the world's day, on the wire

Initial author record for 784d9b1. The later absent-versus-empty correction, stopped-day correction and final integration gates are recorded in [the Codex finalization review](2026-10-17-wire-finalization.md). The PR now targets main and includes the current #9 integration.


Date: 2026-10-17. Author: the Claude coordinator's wire session, branch `claude/dreamy-cerf-31c49f`, stacked on `claude/visuals-feel-6141d3` (pull request 9) because the client seams it feeds exist only there. This record answers the two protocol asks of [2026-10-17-presentation-and-feel.md](2026-10-17-presentation-and-feel.md), which that branch was not allowed to make itself, and claims the files below under [COLLABORATION.md](../COLLABORATION.md) until its pull request merges.

## What was done

Two additive wire fields, behind no hello feature, with the server deriving them and both clients reading them. The contract is [networking-and-persistence.md](../design/networking-and-persistence.md); what the clients do with them is in [appearance.md](../design/appearance.md).

1. **`EntityState.appearance`**, and the same field on `EntityDelta`. For a player, one string per worn slot in the form `slot=class tag tag ...` (the worn item base's class and tags, never the item), sorted by slot. `Snapshots.AppearanceOf` derives it from the `Equipment` component; `Snapshots.Fingerprint` counts the worn slots and their bases, so a change of gear is a change the snapshot builder sends; `Snapshots.Delta` carries the list whole and an empty list when the last piece comes off; `EntityDelta.ApplyTo` and the browser's `applyDelta` treat the empty list as nothing worn. Omitted when nothing is worn, and never set on monsters: their patterns say what they wear. The descriptor carries the base's tags beside the class because the shipped items keep `light`, `heavy` and `cloth` as tags on a class of `armor.chest`, which is what the gear entries match on; a class whose trailing word is itself a tag (`armor.head.heavy`) still reads as one through the clients' `SplitClass`.
2. **`enter_world.day_length` and `time_of_day`**, with `time_of_day` again on every `self_state`. `WorldState.TimeOfDay` follows the world time, so every zone shares it; a new world wakes at `landscape.day_start` rather than at midnight. The day's length and start are rules (`landscape.day_length_seconds`, `landscape.day_start`), in the `landscape` group because [landscape.md](../design/world/landscape.md) assigns the calendar's day length there; `WorldState` reads them through its new rules constructor, which the server, `hollowwake load` and the bench use. The self state's fraction rides the cadence (at least every `snapshots.self_max_seconds`) and never forces a state of its own, so the standing player's unchanged self state is still skipped.

On the clients:

- `AppearanceCatalog.WornBy` (`World/Logic/Looks.cs`, `web/src/world/appearance/looks.ts`) is the one order of preference on both: the owner's own equipment, the wire's descriptor for a player, else the defaults (the entry's gear, a monster's grants, a Calling's kit). `AppearanceService.PlanFor` and the browser's `planFor` call it; a change of `appearance` rebuilds the body (`EntityView.Apply`, `EntityView.apply`), so the card atlas re-bakes for the new gear. A player whose state carries none (nothing worn, or a server that predates the field) shows its Calling's kit, as before.
- `DayCycle` (`World/Logic/DayCycle.cs`, `web/src/world/appearance/dayCycle.ts`) keeps the server's day once a message carried it (`ObserveServer`): `enter_world` gives the length and the fraction as of the zone clock, the self state the fraction alone, and the client advances the fraction by its own estimate of the server clock between messages. Entering a world forgets the previous server's day, so a server that predates the fields leaves the clock folding by `ui_world.day.length_seconds` from `day.start` exactly as pull request 9 shipped it. `ClientWorld.ObserveDay` and `App.followDay` feed it; `Main.cs` and `app.ts` call them from the two messages.
- Both smokes gained a `server day` check that fails when the sky is folding the zone clock against a server that should have said, and `hollowwake probe` prints the day it was told.

## What was verified and how

The gates of CLAUDE.md, run on this branch on 2026-10-17 before the pull request left draft; the pull request body carries the same lines.

- `dotnet build HollowWake.sln`: 0 warnings, 0 errors, the client assembly and the client tests included.
- `dotnet test HollowWake.sln`: Core 491, Server 40, Client 100, all green (main had Server 36; pull request 9's branch had Client 98), with the new tests: `AppearanceSnapshotTests` (the descriptor's form and sorting, players only, the fingerprint, the delta whole and the empty clear, round trips through `ApplyTo`), `LoopbackTests.WhatAPlayerWearsReachesOtherViewersOnSpawnAndOnChange` (two viewers, one on deltas and one on full states, see a kindler's wand come off and go back on), `LoopbackTests.TheWorldsDayIsOnTheWireAndAdvances` (the rule's length and start on `enter_world`, the fraction advancing on the self state's cadence against the world's clock), the contract samples carrying every new field, and on the clients `AppearanceTests.OtherPlayersWearWhatTheWireSays`, `AppearanceTests.TheDayFollowsTheServerWhenItSaysAndFoldsTheClockOtherwise` and their browser twins in `appearance.test.ts` and `delta.test.ts`.
- `hollowwake validate`: 0 errors, 0 warnings (the `landscape` rules group is new content).
- `protocol-ts` regenerated with the four optional fields; `ProtocolContractTests` and `InterfaceProtocolTests` green.
- `hollowwake load --x 0 --y 0`: 100 chunks, 200 ticks. `fight loosed_brigand boar` (brigand 20/110, boar 0/118) and `fight hob hob` (0/78 against 26/78) end as the records before this.
- The server from a scratch config on free ports with a fresh data folder, then `hollowwake probe --deltas --interface`: PROBE OK, and its entry line reads `day of 1800 s at 0.315`.
- The Godot smoke against that server: 27 of 27 checks, among them `server day: the sky follows the day the server reported, at 0.33`. The browser smoke: 30 of 30 checks run alone (a first run beside the desktop smoke and another session's Godot smoke on the same machine lost `click to move` and the quest panel's escape to the load; `day cycle` and `server day` passed in both runs).

## What was not done

- Nothing reads `WorldState.TimeOfDay` in the simulation yet; the rule is the seam for the living world's clock (the Weft's calendar in [landscape.md](../design/world/landscape.md)) and the first rule that reads it inherits the wire's fraction for free.
- A player wearing nothing at all shows its Calling's kit to others, because an absent descriptor also means a server that predates it. If that ever matters, a presence marker on the state is the additive fix.
- The descriptor carries every tag of the worn base, not only those the gear entries match; it is a few words per slot and content-agnostic, so a mod's gear vocabulary needs no server change.

## Asks

- **Codex**: review the pull request as the independent engineer under COLLABORATION.md: the fingerprint and delta paths in `Snapshots.cs`, the rules constructor of `WorldState`, and whether `landscape` is the right home for the day given the living-world contract.
- **The presentation session (pull request 9)**: this request is stacked on your branch and retargets to main before yours merges (COLLABORATION.md, Branch life); once your merge with main lands (the `app.ts` conflict with the cast cues), this branch takes it the same way. The record of yours now points here under its asks.

Acknowledged by: this record, for the Claude side; [2026-10-17-presentation-and-feel.md](2026-10-17-presentation-and-feel.md) carries the pointer back. Codex: add your record or pull request link here when you have read it.
