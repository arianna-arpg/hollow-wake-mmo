using System.Text.Json.Nodes;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class LoadingKinds
{
    // An open set: another presentation activity registers its schema here, never in a simulation system.
    private static readonly Dictionary<string, Action<JsonObject, ValidationContext>> Activities = new()
    {
        ["spirit_run"] = SpiritRun,
    };

    public static void Register() => ContentKinds.Register<UiLoadingScreenDef>("ui_loading_screens", Validate);

    private static void Validate(UiLoadingScreenDef d, ValidationContext c)
    {
        if (d.Meta is not { } meta || meta["activity"]?.GetValue<string>() is not { } kind || !Activities.TryGetValue(kind, out var check))
        {
            c.Error("meta.activity must name a registered loading activity.");
            return;
        }
        check(meta, c);
        if (d.Id == c.Registry.All<UiLoadingScreenDef>().Keys.Min(StringComparer.Ordinal)
            && c.Registry.Each<UiLoadingScreenDef>().Count(x => x.Default) != 1)
        {
            c.Error("exactly one loading screen must be the default.");
        }
    }

    private static void SpiritRun(JsonObject meta, ValidationContext c)
    {
        double Number(JsonNode? node) => JsonNum.TryDouble(node, out var value) && double.IsFinite(value) ? value : double.NaN;
        foreach (var key in new[] { "timeoutSeconds", "seedStep" })
            if (!(Number(meta[key]) > 0)) c.Error($"meta.{key} must be positive.");
        if (meta["controls"] is not JsonObject controls) c.Error("meta.controls is required.");
        else
        {
            if (!(Number(controls["deadzone"]) >= 0 && Number(controls["deadzone"]) < 1)) c.Error("controller deadzone must be within [0,1).");
            foreach (var key in new[] { "negativeX", "positiveX", "negativeY", "positiveY" })
                if (controls[key] is not JsonArray codes || codes.Count == 0 || codes.Any(x => !System.Text.RegularExpressions.Regex.IsMatch(x?.ToString() ?? "", "^(Key[A-Z]|Arrow(Left|Right|Up|Down))$")))
                    c.Error($"controls.{key} must name letter or arrow physical keys.");
        }
        foreach (var key in new[] { "seed", "seedStep" })
            if (!(Number(meta[key]) >= 0 && Number(meta[key]) <= uint.MaxValue && Number(meta[key]) == Math.Floor(Number(meta[key]))))
                c.Error($"meta.{key} must be an unsigned 32-bit integer.");
        if (!(Number(meta["readyRadius"]) >= 0 && Number(meta["readyRadius"]) <= 12 && Number(meta["readyRadius"]) == Math.Floor(Number(meta["readyRadius"]))))
            c.Error("meta.readyRadius must be an integer within 0..12.");
        if (meta["rules"] is not JsonObject rules) { c.Error("meta.rules is required."); return; }
        foreach (var key in "length halfWidth playerU radius baseSpeed speedPerGate maxSpeed steerSpeed gateGapMin gateGapMax gateRim gateThickness gateSpacing choiceStagger pickupRadius pickupFade currentHalfLength currentHalfWidth currentExtra currentSeconds currentEase maxBoostSpeed burstSeconds maxBursts surgeSeconds hinderSeconds hinderSpeed entrySeconds spawnAhead despawnBehind openingCurrentChance betweenCurrentThreshold secondCurrentChance routeMargin choiceBendMin choiceBendRange hinderedSteer burstDrift maxFrame step".Split(' '))
            if (!(Number(rules[key]) > 0)) c.Error($"meta.rules.{key} must be positive.");
        double N(string key) => Number(rules[key]);
        foreach (var key in new[] { "openingCurrentChance", "betweenCurrentThreshold", "secondCurrentChance", "hinderSpeed", "hinderedSteer" })
            if (!(N(key) <= 1)) c.Error($"meta.rules.{key} must not exceed one.");
        if (!(N("openingCurrentChance") <= N("betweenCurrentThreshold") && N("gateGapMin") <= N("gateGapMax") && N("maxBursts") == Math.Floor(N("maxBursts"))))
            c.Error("loading thresholds, opening widths or burst count are inconsistent.");
        if (!(N("radius") < N("halfWidth") && N("playerU") < N("length") && N("currentEase") <= N("currentSeconds")
            && N("maxSpeed") <= N("maxBoostSpeed") && N("step") <= N("maxFrame")))
            c.Error("loading bounds, step, speed caps or current duration are inconsistent.");
        if (meta["gateProfiles"] is not JsonArray profiles || profiles.Count == 0)
            c.Error("meta.gateProfiles must contain gate layouts.");
        else
        {
            double previous = 0;
            foreach (var p in profiles)
            {
                var until = Number(p?["until"]);
                if (!(until > previous && until <= 1)) c.Error("gate profile thresholds must increase to one.");
                previous = until;
                if (p?["lanes"] is not JsonArray lanes || lanes.Count == 0) { c.Error("gate profile lanes are required."); continue; }
                var drift = Number(p?["drift"]);
                var width = Number(p?["maximum"]);
                if (!(drift >= 0 && width >= N("gateGapMin"))) c.Error("gate profile drift or width is invalid.");
                var centers = lanes.Select(Number).ToArray();
                for (var i = 0; i < centers.Length; i++)
                {
                    if (!(Math.Abs(centers[i]) + drift + width / 2 <= N("halfWidth") - N("gateRim")))
                        c.Error("gate profile breaks the outer rim.");
                    if (i > 0 && !(centers[i] - centers[i - 1] >= width))
                        c.Error("gate profile openings overlap or are not ordered.");
                }
            }
            if (previous != 1) c.Error("gate profile thresholds must end at one.");
        }
        if (!(N("gateGapMin") > N("radius") * 4 + N("currentHalfWidth") * 2))
            c.Error("the narrowest opening must allow the body to bypass a current.");
        if (meta["pickups"] is not JsonObject pickups || pickups.Count == 0)
            c.Error("meta.pickups is required.");
        else if (meta["choices"] is not JsonArray choices || choices.Count == 0)
            c.Error("meta.choices is required.");
        else foreach (var choice in choices)
            if (choice is not JsonArray pair || pair.Count != 2 || pair.Any(id => id is null || !pickups.ContainsKey(id.GetValue<string>())))
                c.Error("each choice must name two declared spirits.");
        if (meta["pickups"] is JsonObject styles) foreach (var (_, style) in styles)
        {
            if (string.IsNullOrWhiteSpace(style?["name"]?.ToString())) c.Error("each spirit needs a name.");
            foreach (var key in new[] { "size", "radiance", "flicker" })
                if (!(Number(style?[key]) > 0)) c.Error($"spirit {key} must be positive.");
            var boost = Number(style?["boostGates"]);
            if (!(boost >= 0 && boost == Math.Floor(boost) && boost <= int.MaxValue)) c.Error("spirit boostGates must be a nonnegative integer.");
        }
        RequireKeys(meta["labels"], "brand title accessibleTitle boot entry terrain failed timeout retry cancel hint controls canvas progress", false, c);
        RequireKeys(meta["palette"], "wisp current floor abyss impact gate narrowGate stone stoneDeep", true, c);
        if (meta["runes"] is not JsonValue runeText || !runeText.TryGetValue<string>(out var runes) || string.IsNullOrWhiteSpace(runes))
            c.Error("meta.runes must be a nonempty alphabet.");
        var layers = new HashSet<string>("abyss ribbons gates currents souls bursts wisp".Split(' '));
        if (meta["layers"] is not JsonArray order || order.Count == 0 || order.Any(x => !layers.Contains(x?.ToString() ?? ""))
            || order.Select(x => x!.ToString()).Distinct().Count() != order.Count)
            c.Error("meta.layers must be a unique ordered list of registered painters.");
        if (meta["wisp"] is not JsonObject wisp) { c.Error("meta.wisp is required."); return; }
        if (!(Number(wisp["radius"]) > 0)) c.Error("wisp radius must be positive.");
        RequireKeys(wisp["palette"], "base glow light shadow outline", true, c);
        var parts = new HashSet<string>("disc halo eyes wisps".Split(' '));
        if (wisp["parts"] is not JsonArray recipe || recipe.Count == 0) c.Error("wisp parts are required.");
        else foreach (var part in recipe)
        {
            if (!parts.Contains(part?["kind"]?.ToString() ?? "")) c.Error("wisp part must name a registered painter.");
            if (part?["scale"] is { } scale && !(Number(scale) > 0)) c.Error("wisp scale must be positive.");
            if (part?["params"] is JsonObject parameters && parameters.Any(x => !double.IsFinite(Number(x.Value)))) c.Error("wisp parameters must be finite numbers.");
        }
    }

    private static void RequireKeys(JsonNode? node, string keys, bool colors, ValidationContext c)
    {
        if (node is not JsonObject obj) { c.Error("loading text and palettes must be objects."); return; }
        foreach (var key in keys.Split(' '))
            if (obj[key] is not JsonValue v || !v.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value) || colors && !InterfaceKinds.IsColor(value))
                c.Error($"loading presentation requires a valid {(colors ? "color" : "label")} '{key}'.");
    }
}
