# Contributing

Read `docs/VISION.md` first; it is the test for every change. `CLAUDE.md` has the repository map, commands, verification gates and conventions.

Short version:

- Content before code. If a thing can be a definition, a rule or an expression, make it one.
- One modifier engine, tags everywhere, registries for open sets, events between systems, deterministic randomness, server authority.
- Every system has one contract in `docs/design/`; change the contract with the system.
- Run the gates: build, test, validate, and the headless smokes for anything you touched.
- Keep docs durable: no counts, rosters or version strings that go stale.
