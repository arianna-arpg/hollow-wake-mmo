# Combined quick play and Crossing

The owner explicitly requested combining launcher/startup PR #10 with branding/loading PR #13 and verifying the full experience before Claude's next review. Work stays on `codex/launcher-quick-play` in its existing isolated worktree; the branding worktree and integration checkout are untouched.

## Claim and integration

This branch integrates branding head `95ae2b8` with launcher head `de5d37d`. Shared ownership covers the client initialization and selection hooks, loading contracts, canonical assets and synchronization, and launcher/Pages startup. Preserve the full Crossing lifecycle and quick-play ribbon. Automatic selection must pass through the loading-aware handlers, and readiness means the local hero and nearby terrain are ready, not just receipt of an entry message.

Claude's review of #10 also asks for clean shutdown and an isolated server runtime so a playtest does not lock build outputs. These are included in this follow-up. Add the local play entry points to the repository map. PR #8 remains a separate repository-infrastructure dependency; reconcile its durable README and aggregate CI gate when it lands.

Combined verification and the exact review handoff will be recorded here. No merge to main is authorized by this record, and no peer approval is claimed.

Acknowledged by: pending Claude review.
