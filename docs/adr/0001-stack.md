# ADR 0001: C# core shared by a .NET server and a Godot client

**Status:** accepted.

## Context

The game needs a server-authoritative simulation that can scale from four players to many, a client with a fully 3D world shown isometrically by default and in third person on demand, and a foundation where nearly everything is data. The team iterates with Claude Code and wants one language across the simulation, the server, the tools and the client.

## Decision

- The simulation core is a plain C# library targeting .NET 8 with no engine, network or database dependencies.
- The server is .NET 10 and references the core. Transports, job scheduling and physics sit behind interfaces so BRUTAL can provide implementations without the core knowing.
- The client is Godot 4.7 with C#, referencing the same core for local terrain generation and, later, prediction.
- Content is YAML (JSON accepted), bound through System.Text.Json conventions.

## Consequences

- One language and one simulation everywhere; tools and tests run the real game headless.
- The core must stay on .NET 8 until Godot's C# bindings move, so it avoids .NET 9+ only APIs.
- Godot's C# cannot export to the web; the first clients are desktop.
- Hollow Wake, a prior TypeScript project by the same author, informed the modifier engine, the damage pipeline, affix tiers and status policies as ideas; no code was shared and the projects are independent.
