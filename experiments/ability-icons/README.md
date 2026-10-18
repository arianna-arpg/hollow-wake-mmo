# Shared ability icons

The importer extracts artwork from the pinned prototype revision without changing that checkout. Install its pinned lockfile before regenerating. The importer writes only the reachable namespaced catalog and provenance; authored bindings and style remain separate. Adding an explicit binding makes another prototype illustration reachable on the next import.

The native and browser tests consume tests/fixtures/ability-icons.json for the same identity and exact SVG expectations, including live Thread palettes, hierarchical tags, numeric strings, missing optional rule tags, badges and dormant paint. Native validation checks the fixture's malformed paths and references.

Run node experiments/ability-icons/render.mjs for the focused browser check. It starts a private Vite server and headless browser, uses the production item and seat components, saves its gallery under ignored artifacts/ability-icons/focused, and closes both processes. These surface comparisons are browser evidence; the shared fixtures separately check both implementations.

Build the CPU raster regression with `dotnet build experiments/ability-icons/native-raster`, then run `node experiments/ability-icons/raster.mjs <Godot .NET executable>` (or set GODOT). It runs an isolated headless Godot project with private user data and hidden child processes, linking the production SVG writer. It reproduces the rejected eight-digit black fill, verifies RGB/alpha pixels, and compares the actual dormant Volley and Umbra Dash artwork against six-digit references in both shipped themes. SVGs, PNGs and logs go under ignored artifacts/ability-icons/native-raster. The test does not use the shared preview or any game server and makes no claim about native UI layout or GPU rendering.

The background harness runs both full clients against an isolated server. Headless native smoke covers logic and explicitly skips pixels. Earlier native images are historical evidence; see the dated review-fix record for current gates and limitations.
