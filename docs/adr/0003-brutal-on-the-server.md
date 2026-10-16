# ADR 0003: BRUTAL on the server, behind interfaces

**Status:** accepted.

## Context

BRUTAL (https://github.com/ahwootech/BRUTAL) is a C# framework from RocketWerkz: interop, unmanaged memory, allocation-free collections, a job system, logging, and bindings for RakNet networking, Jolt physics, Vulkan and more. It has no editor, scene graph or renderer, targets .NET 10 only, ships natives for linux-x64, win-x64 and osx-arm64, and its public packages are new. Its licence is MIT with an attribution requirement (credits, a logo before gameplay unless waived, and a NOTICE file).

## Decision

- Use BRUTAL on the **server**, where its strengths apply, and Godot on the client, where BRUTAL's missing renderer and editor would have to be rebuilt.
- Keep the core free of BRUTAL. The server exposes seams: `ITransport` (TCP and loopback today, RakNet through BRUTAL in the .NET 10 host), a job scheduler seam for parallel zone ticks, and the terrain and movement model as the physics baseline with Jolt as a later drop-in for complex geometry.
- The BRUTAL host is a separate project that compiles to a stub when the .NET 10 SDK is absent so the solution always builds.

## Consequences

- The server can run with or without BRUTAL from configuration.
- Arm hosts are out for the BRUTAL host until it ships linux-arm64 natives.
- If the game is distributed, BRUTAL's attribution terms apply to products built with it; a server-only use should be confirmed with Ahwoo, and the non-profit waiver may apply. Track the licence text in the package's LICENSES folder.
