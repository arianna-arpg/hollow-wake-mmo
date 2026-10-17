# ADR 0006: A TypeScript browser client over WebSocket, beside the Godot client

**Status:** accepted.

## Context

The game should be playable from GitHub with nothing installed: a server in a Codespace and a page in the browser, published on GitHub Pages where the plan allows. Godot's C# cannot export to the web (ADR 0001), and the desktop client generates terrain locally with the C# generator, which a browser cannot run. The server is authoritative and the protocol is readable JSON (ADR 0004), so a client in another language only has to speak the messages.

## Decision

- A second client in `web/`: TypeScript, three.js and Vite, presentation only, mirroring the desktop client's views, cameras and input-to-intent mapping.
- A WebSocket transport in the server on `System.Net.HttpListener`, with no extra dependencies: one message per binary frame, the same payloads as TCP. The same listener serves the built web client, so one port carries the page and the game. A composite transport lets the pure host run TCP and WebSocket into one world.
- The browser gets terrain from the server through `request_chunk`, with the chunk's water level added to `chunk_data`; requests are bounded to a radius around the player.
- The TypeScript message types are generated from `HollowWake.Protocol`, and a server test fails while they are stale.
- Client settings live in a `config.json` beside the page rather than in code.

## Consequences

- Two clients to keep in step. Only presentation is duplicated: the protocol module is generated, and each client has its own design contract (`docs/design/client.md`, `docs/design/web-client.md`).
- The browser downloads every chunk it shows, which costs bandwidth and server work the desktop client avoids; a binary encoding or compressed chunks would fall under ADR 0004's single serializer.
- The server speaks plain HTTP and WebSocket. TLS comes from what sits in front of it: the Codespaces forwarder, or a reverse proxy on a VPS.
- `HttpListener` on Windows needs a URL ACL to listen on every interface, so local development falls back to this machine only; Linux hosts and containers have no such limit.
