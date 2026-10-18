# Cast forecast rereview evidence

Source handoff f6c5a03 integrates approved tournament main 6e7e241 and fixes the owner-selected committed-swing approach. The dated review record lists compatibility changes and limitations. Approved account-entry/grammar main 8365775 is integrated in 6edc124. entry-* files record its clean build, 919 .NET tests, 322 browser tests, current protocol, and both clients passing melee/spell smoke with no cleanup errors. The browser forecast images were inspected; native headless explicitly excludes rendered pixels.

- build-release-verified, tests-verified and web-tests-final: source handoff functional gates.
- tournament-final and baseline-tournament-6e7: matching ten-seed, 60-second reports. tournament-versus-main names all six deliberate hash changes; tournament-verified checks the regenerated baseline, not merely its printout.
- bands-final and baseline-bands-6e7: both retain the same four failing win-rate bands. They are not passing balance certification.
- old-base-duels: approved 26b9e46 staging; pre-approach-duels: intermediate revised release/rider policy before preferred-distance admission. duel-approach-distance proves the subsequent approach correction in that same staging. Do not compare their endings to the newer tournament staging as if the setup were identical.
- bench-first and bench-integrated: intermediate failed timing/traffic samples. bench-approach-distance restores traffic limits after the gameplay approach fix but still fails timing.
- bench-final-integration: complete default Release measurement at normal priority before final live clients; combined snapshot 1.056 ms exceeds the unchanged 1 ms budget. Adjacent baseline-bench-6e7 also exceeds full snapshot timing. All failures are retained without a budget waiver. No timing sample proves an exclusive cause or dense-world performance.
- pre-entry-melee files: both clients' source-handoff behavior, before approved account-entry integration. Native headless logs explicitly skip rendered pixels and controller operation.

The temporary active-caster world-scan experiment was reverted; its diagnostic timings are retained locally under artifacts/forecast-rereview and do not describe the published implementation. One diagnostic was launched while a build was still pending and is not a usable performance measurement.

Final coordinated full Release pair: baseline-window-836.txt and bench-window-6ed.txt. Engineering heavy work was held; runs were serial at normal priority with identical commands/settings. Branch behavior3.272 ms and combined snapshots1.085 ms exceed the existing3/1 ms limits. These are the final timing findings; no performance pass is claimed.
