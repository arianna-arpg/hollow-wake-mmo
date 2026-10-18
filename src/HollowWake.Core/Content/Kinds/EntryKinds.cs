using System.Text.Json.Nodes;
using HollowWake.Core.Characters;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Content.Kinds;

internal static class EntryKinds
{
    public static void Spatial(JsonObject m, ValidationContext c)
    {
        foreach (var key in "radius seatGap rankGap dwellRadius bodyRadius dwellSeconds speed turnRate wrapRadius wrapReentry wrapClear units cameraSpan bodyHeight bodyWidth wispRadius maxFrame cameraHeight cameraOffset cameraLead".Split(' '))
            if (!JsonNum.TryDouble(m[key], out var n) || !double.IsFinite(n) || n <= 0) c.Error($"meta.{key} must be positive.");
        double N(string key) => JsonNum.TryDouble(m[key], out var n) ? n : double.NaN;
        if (N("seatGap") < 2 * (N("dwellRadius") + N("bodyRadius"))) c.Error("vessel reaches must not overlap.");
        if (!(N("wrapReentry") < N("wrapRadius"))) c.Error("wrap reentry must be inside the rim.");
        if (!(N("arcTo") > N("arcFrom") && N("arcTo") - N("arcFrom") < 2 * Math.PI)) c.Error("arc must be a crescent.");
        if (m["labels"] is not JsonObject labels) c.Error("entry labels are required.");
        else foreach (var key in "brand title prompt saved new wake create back name pending empty full unavailable disconnect options".Split(' '))
            if (string.IsNullOrWhiteSpace(labels[key]?.ToString())) c.Error($"entry label {key} is required.");
        foreach (var key in "cardWidth cardPadding cardGap cardTitleSize rosterHeight progressWidth progressHeight".Split(' '))
            if (!JsonNum.TryDouble(m["layout"]?[key], out var n) || !double.IsFinite(n) || n <= 0) c.Error($"meta.layout.{key} must be positive.");
        c.Ref<UiLoadingScreenDef>(m["wispSource"]?.ToString() ?? "", "meta.wispSource");
        if (!(N("apparitionOpacity") > 0 && N("apparitionOpacity") <= 1)) c.Error("apparitionOpacity must be within (0,1].");
        foreach (var key in new[] { "floor", "ink", "accent", "glow", "edge", "panel", "border", "muted", "control", "controlBorder", "footer" })
            if (!System.Text.RegularExpressions.Regex.IsMatch(m["palette"]?[key]?.ToString() ?? "", "^#[0-9a-fA-F]{6}$")) c.Error($"entry palette {key} is invalid.");
    }

    public static void Register()
    {
        ContentKinds.Register<EntryFlowDef>("entry_flows", (d, c) => {
            if (d.CharacterLimit < 1) c.Error("character_limit must be positive.");
            if (d.Stages.Count == 0 || d.Stages.Select(s => s.Id).Distinct().Count() != d.Stages.Count) c.Error("stages must be nonempty with unique ids.");
            foreach (var s in d.Stages)
            {
                if (string.IsNullOrWhiteSpace(s.Id) || !EntryStageKinds.Has(s.Kind)) c.Error("entry stage must have an id and registered kind.");
                c.Ref<UiEntrySceneDef>(s.Presentation, "presentation");
            }
            if (d.Stages.LastOrDefault() is not { } last || !EntryStageKinds.AllowsSelection(last.Kind) || !last.CompletedBy.IsEmpty) c.Error("entry must end in a persistent selection stage.");
        }, validateCollection: c => {
            // Partial tooling packs need no account flow; a playable Calling catalog always does.
            if ((c.Registry.Each<CallingDef>().Any() || c.Registry.Each<EntryFlowDef>().Any())
                && c.Registry.Each<EntryFlowDef>().Count(x => x.Default) != 1)
                c.Error("exactly one entry flow must be default when Callings or entry flows are present.");
        });
        ContentKinds.Register<UiEntrySceneDef>("ui_entry_scenes", (d, c) => {
            if (d.Meta is not { } m) { c.Error("entry presentation needs meta."); return; }
            if (!EntrySceneSchemas.Validate(m["activity"]?.ToString() ?? "", m, c)) c.Error("unregistered entry activity.");
        });
    }
}

/// <summary>Presentation schemas grow beside their paired adapters, without changing account-stage policy.</summary>
public static class EntrySceneSchemas
{
    private static readonly Dictionary<string, Action<JsonObject, ValidationContext>> Schemas = new(StringComparer.Ordinal) {
        ["spatial_vessels"] = EntryKinds.Spatial,
    };
    public static void Register(string activity, Action<JsonObject, ValidationContext> validate) => Schemas.Add(activity, validate);
    public static bool Validate(string activity, JsonObject meta, ValidationContext context)
    {
        if (!Schemas.TryGetValue(activity, out var validate)) return false;
        validate(meta, context); return true;
    }
}
