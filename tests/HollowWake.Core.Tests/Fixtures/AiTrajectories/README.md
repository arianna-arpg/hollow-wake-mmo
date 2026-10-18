# AI trajectory baseline

These three traces were generated on Windows x64 with .NET 10 from main commit `a810b0a2a9a588db8a3959f14f4ddb1d38147e3d`, before AI movement used intents. They were not recorded from the implementation under test. Each row is `tick X Y Z remaining-path-points`, with round-trip precision for positions and invariant number formatting.

The scenario implementation is `AiTrajectoryTests.Trace`: arena seed 3, a hunter held in chase by taunt memory, a durable non-thinking boar, and the three authored obstacle layouts. The moving boar changes its axis-aligned direction every two seconds. No actor enters wandering; that is asserted during recording and comparison. Facing is intentionally absent because the platform's `Atan2` can differ in its last bits. No coordinate tolerance or rounding is used.

To reproduce, create a worktree at the exact baseline commit, copy `AiTrajectoryTests.cs` there, and replace only the fixture-reading/comparison block after `var actual = Trace(scenario)` with `File.WriteAllLines(outputPath, actual)`, choosing one output file per scenario. Run `dotnet test tests/HollowWake.Core.Tests -c Release --filter FullyQualifiedName~AiTrajectoryTests`. The three rows must pass, then compare the generated files by line. The final tests have no fixture-update mode.

The original unmodified scenarios also included random wandering and facing; their Windows hash evidence remains in the intent-seam review record. These fixtures replace that platform-dependent gate with targeted chase coverage. They do not establish cross-platform determinism for all combat or world generation. Failures report the scenario and first differing tick.
