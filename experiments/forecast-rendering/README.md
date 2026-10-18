# Shared forecast rendering evidence

The fixture generator calls the live combat shape predicates and writes sampled membership to `tests/fixtures/cast-forecasts/shapes.json`. Both client adapters consume those same fixtures. The C# client test additionally checks the fixtures against the current combat predicates and compares the complete registered vocabulary, so a newly registered shape or changed collision meaning cannot silently escape the gate.

```powershell
dotnet run --project experiments/forecast-rendering/ForecastFixtures.csproj -c Release -- tests/fixtures/cast-forecasts/shapes.json
dotnet test tests/HollowWake.Client.Tests -c Release
npm --prefix web test
```

Pass restore options to a separate build/restore command, not after `dotnet run`'s `--`. Tests exclude samples within 0.03 world units of a mathematical boundary because finite polygons approximate curves. Thin hollow variants catch accidental cosmetic minimum sizes. Runtime tessellation uses a configurable chord-error bound and declines regions that exceed its segment budget.

These fixtures prove sampled geometry agreement, not terrain clipping, complete graph coverage, prospective victim selection, damage, network-latency tolerance, or crowd readability. Primary regions remain conditional. Unknown delivery families have explicit unavailable spatial coverage.

Live smoke evidence uses normal input and actual server messages. The smoke scripts accept `--forecasts`: the chosen calling's second slot supplies the current area cast; no preset-specific rendering code or synthesized messages are involved. The spell and melee captures exercise distinct content through the same path. Smoke combat success counts actual damage dealt, so a miss log cannot satisfy it.

See the [dated integration handoff](../../docs/reviews/2026-10-17-shared-cast-forecasts.md) for commands, measurements, behavior changes and remaining scope. Generated builds and local server/account data remain ignored.
