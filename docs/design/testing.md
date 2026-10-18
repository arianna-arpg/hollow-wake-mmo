# Background testing

Routine automation must leave the operator's desktop input available. `scripts/test-background.mjs` runs the existing smoke scenarios against a private local server without opening client windows or sending operating system mouse or keyboard input. It is tooling, with no gameplay or protocol changes.

## Run

From the checkout being tested, install the pinned browser dependencies once:

```sh
npm --prefix web ci
node scripts/test-background.mjs
```

The default suite builds the server and browser client and runs the browser smoke headlessly. Playwright sends input to its own browser page; it can capture screenshots while the operator continues using other applications. The runner uses the installed Edge on Windows or Chrome elsewhere. `--browser` or `--executable` selects another installed compatible browser, using the smoke script's existing options.

For native behavior checks, supply the repository's required .NET Godot executable:

```sh
node scripts/test-background.mjs --suite godot --godot /path/to/godot
node scripts/test-background.mjs --suite all --godot /path/to/godot
```

`GODOT` can supply the executable path instead. On Windows the runner also detects a WinGet .NET installation matching the client project's SDK version; otherwise it tries `godot` on PATH. The native suite builds and imports the project, runs with Godot's headless display driver, and injects pointer motion with `Input.ParseInputEvent`. It never calls `Input.WarpMouse`. Native headless mode does not exercise the renderer and does not produce screenshots. Native rendering, GPU effects, focus transitions and OS integration still need a graphical smoke run in an isolated VM, another computer or a dedicated desktop session when the operator needs their desktop free. A second monitor alone does not isolate the cursor. The runner does not provision a VM or schedule recurring work.

`--calling ID` selects the smoke character's Calling. `--timeout SECONDS` bounds each build or smoke stage. `--no-build` reuses this checkout's existing Debug server and client outputs. `--out DIR` selects the evidence parent; each invocation creates a fresh subdirectory, so previous evidence is retained. Arguments not recognized by the runner fail, including `--headed`.

## Isolation and lifecycle

- Each invocation receives a unique evidence folder, native profile, server name, free TCP and HTTP ports, and server data directory. Each suite gets its own account, so native tests never inherit the browser character's position. The runner copies the committed server configuration and overrides only its runtime identity, loopback bind, ports, data directory, web root and development authentication. It never reads `server.local.yaml` or attaches to an existing game server.
- Readiness checks the service identity and this run's unique server name. Another process taking a port between reservation and launch causes failure, never a test against someone else's world.
- The browser has a fresh Playwright context. On Windows and Linux, native user data is rooted inside the evidence directory through the child process environment, including interface placement. Native runs on macOS are rejected until equivalent isolation is implemented.
- Child processes start with hidden Windows console windows. Success, failure, timeout and handled interruption close only the runner's owned process trees. Saved test data remains for diagnosis. An externally force-killed runner cannot execute cleanup; use Ctrl+C or SIGTERM to stop it normally.
- Engineering sessions continue using their own worktrees. Build and Godot import outputs are local to the checkout, so do not build or import concurrently in the same checkout. Concurrent browser runs may use `--no-build` after the build finishes. This isolates input and data, not CPU, memory or GPU usage; demanding suites still share machine resources.

## Evidence and coverage

`artifacts/testing/run-*/result.json` records the overall outcome, stage exit codes, completed smoke check counts, timestamps, private server endpoints and cleanup errors. Stage output goes into separate log files; browser screenshots go under `shots/`. The process exits nonzero on failed or incomplete smoke results, startup failures, timeouts or cleanup failures. An exit code alone cannot pass a smoke: its completed passing summary must also be present.

The runner reuses the scenarios maintained in [client.md](client.md) and [web-client.md](web-client.md). It is not a replacement for the build, unit tests and content gates in [CLAUDE.md](../../CLAUDE.md). Its lifecycle regressions run with:

```sh
node --test scripts/test-background.test.mjs
```

The input distinction follows Godot's [Input.parse_input_event contract](https://docs.godotengine.org/en/stable/classes/class_input.html#class-input-method-parse-input-event): injected events do not move the operating system cursor. Graphical smoke runs still have windows and can receive real input; use the headless runner for concurrent desktop work.
