# Held surface forecasting integration

The complete Fauna foundation `1bbaa120730376e1297c4f4a7011291c1f69b542`, including corrected projectile foundation `0c610e4715c5d32002d051eec55d4fa7f7f4f526` and main `f7ef072cc322b93b628229916c5220024e74a89c`, is merged at `b1a954d05cfc8730e628062fd0a56914e1e91e23`. The merge retains #20's sample-first geometry, physical CastSource input, pre-payload echo capture, committed-direction distinction, card/harness fixes and bounded capture scripts. The existing #20 replay fixture is retained pending actual-union measurement, rather than replaced with isolated foundation hashes.

The held-aim conflict cannot take the incoming mutating `AimAtRelease` implementation into observation. It also cannot simply consume the stored point: #36 resolves the current surface again on release, and a layer can change after accepted input. The downstream correction factors one nearest-surface calculation behind generating `AimGroundAt` and loaded-only `TryAimGroundAt`. Runtime and observation supply their resolved points to the same pure release tuple construction. The existing fresh-input/ground-or-direction predicate is shared. Missing observational terrain, including an interpolation neighbour, produces `unloaded_release_surface` geometry coverage while retaining timing. Authoritative release retains terrain loading.

This is shared terrain/casting behavior. It adds no skill, species, support or visual-theme dispatch. Ordinary timed commitment, absent/stale aim, frozen echo capture, body-sample precedence, physical caster ownership and existing target admission remain unchanged. Surface selection preserves base/layer tie ordering and walkable filtering. No protocol field or client-invented geometry is introduced.

## Focused evidence

Logs are under [held-surface](../../experiments/forecast-rendering/evidence/held-surface/). The first fixture compilation failed because it referenced internal cast fields directly; that diagnostic is retained. The fixture now observes those fields through reflection without touching the runtime visibility contract.

Before the runtime correction, the new suite records 12 failures and 6 passes: changed/removed layers retained old aim height and unloaded terrain still claimed primary geometry. The final focused command passes 144 tests, including all 18 new cases:

```text
dotnet test tests/HollowWake.Core.Tests/HollowWake.Core.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~HeldGroundForecastTests|FullyQualifiedName~CastForecastTests|FullyQualifiedName~LayeredProjectileGroundTests|FullyQualifiedName~SampledAimPrecedenceTests|FullyQualifiedName~AimWrapperElevationTests|FullyQualifiedName~ConstructEchoAimSnapshotTests|FullyQualifiedName~PositionalReleaseTests
```

The new cases cover Ground/Direction, charge/overcharge, changed/removed layers after accepted `Aim`, absent/stale aim, ordinary timed commitment, sampled-offset clearing and actual release points. Missing-cell and missing-neighbour cases retain timing, decline geometry and let authoritative release load. Repeated observation preserves cast/book scalar state, cast identity, scope/modifier references, simulation/cast RNG state, loaded chunk set/count and terrain memo, resources, cooldowns, scheduler count and events. Stale aim is accepted before the cast through the real input path. The existing target, echo and admission suites pass alongside these cases. Changed source/doc whitespace checks pass; raw evidence inherited from #34 retains its original whitespace and captured conflict markers.

Independent forecasting review is requested on the exact published correction before the final combined build/test/client/probe/replay cycle. These focused results are not final functional or performance acceptance. No benchmark, trace or main merge has run; prior 225 and partial 295 records remain untouched.

Acknowledged by: [forecasting peer, 202 passing focused checks](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20#issuecomment-6257391327) and [coordinator's scoped review](https://github.com/arianna-arpg/hollow-wake-mmo/pull/20#issuecomment-6257393230). Full combined acceptance is recorded separately; Claude reciprocal review and performance remain open.
