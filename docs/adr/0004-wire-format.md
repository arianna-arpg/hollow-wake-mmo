# ADR 0004: Readable JSON messages with length framing, serializer behind one class

**Status:** accepted.

## Context

Four players on a development server benefit more from a protocol that can be read by eye than from saving bytes. The protocol will later need delta snapshots and a binary encoding for scale.

## Decision

- Messages are records with a type discriminator, serialized as JSON with the content conventions, framed by a 4-byte little-endian length.
- `MessageSerializer` is the only place that knows the encoding. Transports move opaque payloads.
- Entity updates are full states for visible entities at the snapshot rate; spawns and despawns bracket visibility by interest radius.

## Consequences

- Any language can write a client or a bot today.
- Swapping to a binary encoding or delta snapshots is a change in one class plus the snapshot builder, not in the messages or the systems.
