# Mu selection evidence

The reference is `arpg-game-world` at `118b510f`. Ring-layout reference samples are generated directly from `src/engine/muRing.ts` by `node scripts/mu-reference.mjs <prototype-checkout>`. The samples are shared by native and browser tests.

Build the solution and web client, then run:

```
node experiments/mu-selection/check.mjs
node scripts/test-background.mjs --suite all --no-build --out experiments/mu-selection/artifacts
```

Both commands use private server data and page-local input. The first checks actual Mu selection, touch movement and dwell, latching, keyboard motion, suspension under Options, modal focus, server refusal, a delayed creation reply after cancellation, portrait layout, reconnect, controller selection and the real Crossing handoff. It also runs the protocol probe. The second runs the ordinary play-throughs through native and browser Wake cards.

`evidence/` holds small browser captures and the final gate reports. Full logs, native import output and test saves remain under ignored `artifacts/`. Native testing uses the headless driver: behavior and control layout are checked; native rendered pixels require an isolated graphical session and are not claimed here.
