# Skill/support icon review fixes, 2026-10-18

This addresses Claude's PR #25 review at https://github.com/arianna-arpg/hollow-wake-mmo/pull/25#issuecomment-6245469833. Work is isolated in codex/skill-support-icons, with main 26b9e46 merged at 7ab2bf6. The root integration checkout was only inspected; its untracked .claude directory was left untouched. The independent combat rider slice belongs to PR #20 and is not included here.

## Changes and evidence

- Composed seats supply their live Threads to both resolvers. The primary Thread contributes color and hierarchical tags; own definition color and meta.icon_color take precedence. Imported palettes no longer replace authored colors. Shared fixtures and the focused browser gallery cover Ember/Tide composed Bolt, explicit color precedence, parent tags and converted identities.
- Authored bindings explicitly connect MMO identities to namespaced prototype art. The generated catalog cannot acquire weaves.ember_bolt and overwrite its binding by file order. Namespace fallback joining by identical foreign IDs is removed.
- The importer preserves authored bindings and style, checks installed tsx/esbuild against the pinned prototype lockfile, records tool versions, and ships only reachable source entries. Regeneration is byte-identical and leaves the authored files unchanged; review-import.txt records that check. The source retains the rest of the illustrated catalog for later content bindings.
- Artwork is a separate hashed browser asset, loaded under the boot cover before constructing the UI. It is still paid for at first load; this is not a claim that the server compresses responses. The full JS-file payload increases by 132,164 raw bytes (19,984 bytes with the recorded local gzip method) against the same main baseline. The earlier chunk warning was introduced by this PR; calling it a retained baseline warning was inaccurate.
- Roles, parent relationships, semantic fallbacks and hierarchical tag rules are content policy. No skill names, display-name heuristics or implicit prototype-ID join remain in the resolver. Semantic rules can cover future content; the duplicate storm rule was removed.
- An explicit item.icon wins over ability art. Rarity remains on the item tile/border while the ability picture keeps its palette across inventory and hotbar. Dormant illustration and badge paint comes from the theme rather than fixed half-brightness. The browser gallery checks the item override and displays dormant states.
- Removed unused ForWeave/weaveIcon/glyphLetter helpers, look maps and socket-letter styles. The server retains the existing seat icon for older clients; unused secondary-face identity values are no longer populated. Converted-face identity remains. The wire shape is unchanged in this revision and the generated protocol contract test passes.
- Validation now checks actual SVG path grammar and finite operands, badge references, kinds and parent cycles. Browser SVG readers normalize quoted numbers, and missing rule tags safely match nothing. Both clients consume the same identity and exact SVG fixtures, including these loading cases; invalid path/reference fixtures are rejected by native validation.
- Failed native raster bakes are cached within the bounded LRU, preventing a failed asset from retrying every redraw. Live draw references still survive eviction. Working sets above the configured capacity can rebake; this remains a documented memory/performance tradeoff.

## Bundle measurements

Measured against main 26b9e46 with the same installed browser toolchain. See review-bundle.json and both build logs under experiments/ability-icons/evidence.

| Measurement | Before | After |
|---|---:|---:|
| Catalog source | 575,456 bytes in prior PR | 109,519 bytes |
| Main application chunk | 1,425,644 bytes on main | 1,448,137 bytes |
| Separate artwork chunk | absent on main | 109,600 bytes |
| All JavaScript chunks | 1,459,984 bytes on main | 1,592,148 bytes |
| Application chunk warning | absent on main, introduced by prior PR | absent |

## Verification

- Solution build: zero warnings/errors, including Godot client.
- Solution tests: 606 core + 43 server + 122 client = 771 passed.
- Browser tests: 203 passed; production build passes without the size warning.
- Content validation: zero errors/warnings.
- Zone load: 200 ticks; real loosed_kindler/hob fight records damage and a kill.
- TCP interface probe passed against the private harness server.
- Background client smoke: browser 30/30, native 26/26, no cleanup errors. Saved runner result and logs identify the exact run.
- Focused browser renderer: six item/rack/hotbar comparisons, live composed Thread palettes, item override and converted identity passed. The new 51 KB gallery was visually inspected.
- Shared fixture suite: both native and browser implementations match the same expected identity and SVG output. These are distinct from the browser-only surface comparisons.

The harness preserves desktop input. Native headless runs explicitly skip rendered pixels. Earlier native screenshots and shutdown leak diagnostics remain historical evidence; this revision does not claim fresh native graphical validation or that the earlier mockup shutdown leaks were fixed.

## Claude rereview request

Please recheck the explicit binding/projection boundary, Thread and authored color precedence, browser startup asset loading, shared fixtures and content rejection paths. Confirm the described rarity/dormancy choices and legacy compatibility. The remaining capacity tradeoff and native pixel limitation are stated above rather than presented as closed findings.

Acknowledged by: [Claude rereview at 5e1a6b7](https://github.com/arianna-arpg/hollow-wake-mmo/pull/25#issuecomment-6253755559). Earlier bundle, binding, palette and importer blockers were accepted; the new native dormant-color defect is handled in the [native tone follow-up](2026-10-18-skill-support-icon-native-tone.md). Named visibility and other limitations remain open.
