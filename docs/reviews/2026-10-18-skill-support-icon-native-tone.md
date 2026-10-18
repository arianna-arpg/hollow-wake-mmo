# Native dormant-icon tone fix, 2026-10-18

This follows [Claude's rereview of PR #25 at 5e1a6b7](https://github.com/arianna-arpg/hollow-wake-mmo/pull/25#issuecomment-6253755559). All work remains in codex/skill-support-icons. The root checkout, coordinator work and shared preview were not changed.

## Blocking defect addressed

Godot Color.ToHtml() supplies an alpha byte by default. The prior SVG writer embedded that eight-digit hex directly, which Godot's SVG loader rasterized as black. The fix is at the shared SVG paint boundary: both writers emit RGB and a separate fill-opacity or stroke-opacity for eight-digit hex. This applies to theme tones, authored meta.icon_color/palettes, the wash, ink, rims and badge paint. Wash opacity still multiplies the color's alpha.

Native call sites deliberately keep full RGBA rather than switching to ToHtml(false) and discarding an authored theme alpha. The normalized writer accepts that input safely. Existing three- and six-digit paints retain their output. Identity selection, palette precedence and artwork bindings are unchanged; there is no skill-specific rendering behavior.

Six added shared cases cover opaque, translucent and transparent dormant tones, authored meta alpha, and translucent style/badge/background/outline paint. Both client implementations compare against the same expected SVG strings.

## Native raster evidence

The isolated project under experiments/ability-icons/native-raster links the production AbilityIcons, Json and Rgba source. Build it with dotnet build experiments/ability-icons/native-raster, then run node experiments/ability-icons/raster.mjs <Godot .NET executable> (or set GODOT). Its children are hidden and headless, with private user data; no server, shared preview, desktop input or graphical game window is used.

Godot's actual CPU Image.LoadSvgFromString loader passed 14/14 checks. The control reproduces black from a raw #77736cff rectangle. The fixed output returns pixel 77736cff, preserves translucent authored/tone alpha within raster quantization tolerance, and preserves alpha zero. Actual dormant Volley and Umbra Dash art matches its six-digit reference pixel-for-pixel in both shipped themes, with more than a thousand visible theme-painted pixels per image.

The saved PNGs were visually inspected. This is fresh native SVG raster evidence, not native UI layout, texture draw, GPU, readability-at-every-size or graphical mockup coverage. Earlier native mockup shutdown leaks are not claimed fixed.

## Gates

- dotnet build HollowWake.sln: zero warnings/errors, including the Godot client.
- dotnet test HollowWake.sln --no-build: 606 core + 43 server + 122 client = 771 passed.
- dotnet run --no-build --project src/HollowWake.Tools -- validate: zero errors/warnings.
- npm --prefix web test: 209 passed, including the added shared cases.
- npm --prefix web run build: passed without a chunk size warning.
- The standalone raster project builds cleanly and its headless loader checks pass 14/14.
- node scripts/test-background.mjs --suite all --no-build --out artifacts/ability-icons/native-tone-smoke --timeout 180: browser 30/30 and native 26/26; private server stopped, cleanup errors empty. Native smoke explicitly skips graphical pixels.

Current evidence is native-tone-* under experiments/ability-icons/evidence. This narrow follow-up changes presentation output and regression tooling only; gameplay, wire, importer and benchmarks were not rerun or changed. Prior migration-level load/duel/probe/import evidence remains historical, not relabeled as a fresh run.

## Retained limitations from the rereview

- Ink-only bound forms still differ by the 16 percent background wash, and the opaque icon square still covers the Thread halo. Thread washes read content colors rather than the theme's per-Thread palette; Void remains hard to distinguish. No art selection, wash contrast or halo visibility remedy is claimed.
- The separate artwork fetch remains serial at first boot; the previously measured extra raw JavaScript payload was about 132 KB. This fix does not add server compression or change startup ordering.
- Binding validation still does not check whether every kind.id names a live definition. Empty rule kinds/tags can validate without firing. Import pruning still begins with bindings; style-only/meta-only art references fail validation if pruned. The importer checks tsx/esbuild, not the recorded Node version.
- Native drag ghosts still omit dormancy, unlike the browser ghost. Missing-theme dormant fallback choices remain different across call sites.
- bundledKinds() can still freeze an art-less catalog if called before content preparation.
- Previously named stale comments/docs and the historical evidence size/local paths remain. New retained logs redact local paths. Bounded texture-cache working sets can still rebake, and graphical native presentation remains unverified in this follow-up.

## Rereview request

Claude: recheck the RGB/opacity boundary, full-alpha preservation and the shared/raster regressions against the new head. The blocking black-fill defect is the only finding claimed fixed here; the accepted earlier fixes and the limitations above retain their previous status.

Acknowledged by: pending Claude rereview.
