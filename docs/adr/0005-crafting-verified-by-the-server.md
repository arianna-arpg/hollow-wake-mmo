# ADR 0005: Crafting minigames are seeded challenges scored by the server

**Status:** accepted.

## Context

Crafting should be a minigame, but a server-authoritative game cannot trust a client's claim of "I did well".

## Decision

- A recipe names a procedure (timing, sequence, alignment, or none; more by registration). On start the server creates a seeded challenge from the procedure's parameters and sends it to the client.
- The client plays it however it likes and submits raw inputs (times, choices, values).
- The server scores the same challenge from the same data, maps score to quality through the recipe's expression and the crafter's attributes, consumes inputs, mints the output with quality driving rarity and local values.

## Consequences

- The client can be as pretty as it wants; the outcome is the server's.
- A time limit with a grace period bounds how long a challenge stays open.
