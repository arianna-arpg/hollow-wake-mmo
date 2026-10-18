# Background testing

Routine automation must leave the operator's desktop input and active window focus available. `scripts/test-background.mjs` runs the existing smoke scenarios against a private local server without opening client windows or sending operating system mouse or keyboard input. It is tooling, with no gameplay or protocol changes.

## Run

From the checkout being tested, install the pinned browser dependencies once:

```sh
npm --prefix web ci
node scripts/test-background.mjs
```

The default suite builds the server and browser client and runs the browser smoke headlessly. Playwright sends input to its own browser page; it can capture screenshots while the operator continues using other applications. The runner passes `--isolated-input` to suppress physical controller polling and connection events inside that page, because headless Chromium otherwise still receives real gamepad input. The operator's devices remain available to other applications. These runs do not test controller support. The runner uses the installed Edge on Windows or Chrome elsewhere. `--browser` or `--executable` selects another installed compatible browser, using the smoke script's existing options.

For native behavior checks, supply the repository's required .NET Godot executable:

```sh
node scripts/test-background.mjs --suite godot --godot /path/to/godot
node scripts/test-background.mjs --suite all --godot /path/to/godot
```

`GODOT` can supply the executable path instead. On Windows the runner also detects a WinGet .NET installation matching the client project's SDK version; otherwise it tries `godot` on PATH. The native suite builds and imports the project, runs with Godot's headless display driver, and injects pointer motion with `Input.ParseInputEvent`. It never calls `Input.WarpMouse`. Native headless mode does not exercise the renderer and does not produce screenshots. Native rendering, GPU effects, focus transitions and OS integration still need a graphical smoke run in an isolated VM, another computer or a dedicated desktop session when the operator needs their desktop free. A second monitor alone does not isolate the cursor. The runner does not provision a VM or schedule recurring work.

`--calling ID` selects the smoke character's Calling. `--timeout SECONDS` bounds each build or smoke stage. `--no-build` reuses this checkout's existing Debug server and client outputs. `--out DIR` selects the evidence parent; each invocation creates a fresh subdirectory, so previous evidence is retained. Arguments not recognized by the runner fail, including `--headed`.

## Isolation and lifecycle

Add `--forecasts` to exercise shared forecast reception, or `--cast-cues` for the confirmed visual sequence; both flags pass through to either client. For example, `node scripts/test-background.mjs --suite godot --calling holdfast --forecasts` checks melee forecasting, and `--calling kindler --forecasts --cast-cues` checks spell forecasting and cue lifecycle. Headless native runs still do not validate rendered pixels.

- Each invocation receives a unique evidence folder, native profile, server name, free TCP and HTTP ports, and server data directory. Each suite gets its own account, so native tests never inherit the browser character's position. The runner copies the committed server configuration and overrides only its runtime identity, loopback bind, ports, data directory, web root and development authentication. It never reads `server.local.yaml` or attaches to an existing game server.
- Readiness checks the service identity and this run's unique server name. Another process taking a port between reservation and launch causes failure, never a test against someone else's world.
- The browser has a fresh Playwright context. On Windows and Linux, native user data is rooted inside the evidence directory through the child process environment, including interface placement. Native runs on macOS are rejected until equivalent isolation is implemented.
- Child processes start with hidden Windows console windows. The detected Godot installation uses its engine executable directly, with output piped to logs. Both clients are headless, so tests never request foreground focus, minimize the operator's window, or activate another window. Success, failure, timeout and handled interruption close only the runner's owned process trees. Saved test data remains for diagnosis. An externally force-killed runner cannot execute cleanup; use Ctrl+C or SIGTERM to stop it normally.
- Engineering sessions continue using their own worktrees. Build and Godot import outputs are local to the checkout, so do not build or import concurrently in the same checkout. Concurrent browser runs may use `--no-build` after the build finishes. This isolates input and data, not CPU, memory or GPU usage; demanding suites still share machine resources.

## Evidence and coverage

`artifacts/testing/run-*/result.json` records the overall outcome, stage exit codes, completed smoke check counts, timestamps, private server endpoints and cleanup errors. Stage output goes into separate log files; browser screenshots go under `shots/`. The process exits nonzero on failed or incomplete smoke results, startup failures, timeouts or cleanup failures. An exit code alone cannot pass a smoke: its completed passing summary must also be present.

The runner reuses the scenarios maintained in [client.md](client.md) and [web-client.md](web-client.md). It is not a replacement for the build, unit tests and content gates in [CLAUDE.md](../../CLAUDE.md). Its lifecycle regressions run with:

```sh
node --test scripts/test-background.test.mjs
```

On Windows, `pwsh -NoProfile -File experiments/background-testing/check-focus.ps1` runs both suites and samples the foreground window, failing if a sampled window belongs to the runner's process tree. The probe only reads focus; it does not activate windows. This is supporting evidence, not a guarantee about transitions shorter than the sampling interval.

The input distinction follows Godot's [Input.parse_input_event contract](https://docs.godotengine.org/en/stable/classes/class_input.html#class-input-method-parse-input-event): injected events do not move the operating system cursor. Graphical smoke runs still have windows and can receive real input; use the headless runner for concurrent desktop work.

## The morph test

The morph test (`tests/HollowWake.Core.Tests/Grammar`) checks the combat grammar's invariants at points sampled across the space of casts, rather than at chosen presets. Each delivery and agency family has a parameter table: its axes, each one quantity with a unit inside a dimension of the grammar, with a range reaching the largest value shipped content gives it within the run's cap (a range kept shorter states why in the table) and widened to the handler's clamp, and its choices, each a word of a registered set, read from the registry where the core keeps one. The test samples points from each table on a constant seed, writes every point as a weave on a shell form and a caster pattern wearing it in a scratch content layer loaded beside core, and presses it in a seeded arena facing a fixed crowd of enemies, one of them hurt, with an ally of the caster among them: once, or for a summon or a deploy twice, the second press after the first one's cooldown, where the cap on what stands bites.

Every point must validate (any issue the scratch layer raises fails it), compile strictly and state what it is as mechanisms, both ways: a registered mechanism whose answer the table's samples move must hold exactly at the points that should state it, as its own description says. Every pressed point must replay identically on its seed, lose no life outside the one damage pipeline, stay within the fan-out its parameters and the delivery rules allow (the hits the caster or anything the press made struck, what stands at once, what it made, and how often each marked list runs, a flight's ticks and travel among them), end what it made within its expected life (worked out to the tick from its parameters and the rules, a shove's from the displacement system's own settle time, a return's from its distance over its speed) and a few ticks more, leave nothing behind but summons and constructs that their lifetime, their contract or their owner's cap will end (a group standing no more than the capacity its summon gives the owner, after every press), have paid its cost and started its cooldown by its first resolution (publishing its cast once, and paying nothing more over the run), and fly a salvo's last beat on the tick its interval names. Sampled points also carry riders that reach past a hit, each marking the effect depth it runs at: a drain through the resource effect, a status that ticks, a cast of another weave, and a relay seat that casts itself when its caster casts, with a follow-up and a proc that answers every cast; what a resolution sets going must run deeper than it.

Then each axis, moved alone from a default sample (and from a further row where another kind reads it its own way, as a shove reads a distance), must change something the crowd shows; the words of each choice, compared two at a time from the default sample, must show the crowd apart, a word that rolls on the cast's stream compared over several of the point's seeds rather than one draw; and an axis with a weave attribute behind it must read the same through the attribute as through the node, a door the attribute catalog clamps compared with the node at the value it holds, the clamp itself listed against the grammar's rule 1.

Defects it finds stay listed in the test with their reasons until they are fixed, and so do the baseline exceptions the grammar names (the resource effect for invariant 1, the bare spawn for rule 7, the depth restarts for rule 8); a parameter, or a pair of words, the crowd cannot show apart is exempt only with a stated reason. Setting `HOLLOWWAKE_MORPH_POINTS` runs a deeper sweep of the same streams.
