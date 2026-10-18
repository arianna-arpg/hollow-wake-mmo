using System.Text.Json.Nodes;
using Godot;
using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;

/// <summary>CPU SVG raster checks using the production writer and Godot loader, with no game window or server.</summary>
public partial class RasterCheck : Node
{
    private string _out = "";
    private int _passed;

    public override void _Ready()
    {
        try
        {
            var root = Path.GetFullPath("../../..", ProjectSettings.GlobalizePath("res://"));
            var args = OS.GetCmdlineUserArgs();
            var output = Array.IndexOf(args, "--out");
            _out = output >= 0 && output + 1 < args.Length ? args[output + 1] : Path.Combine(root, "artifacts/ability-icons/native-raster");
            Directory.CreateDirectory(_out);
            Check(DisplayServer.GetName() == "headless", "headless display isolation");
            var raw = ContentLoader.LoadRaw(new[] { Path.Combine(root, "content/core") }, new ValidationReport());
            var icons = AbilityIcons.Load(kind => raw.TryGetValue(kind, out var entries)
                ? entries.Values.Select(r => r.Object) : Enumerable.Empty<JsonObject>());

            // Reproduce the rejected eight-digit paint before testing the shared writer.
            using (var old = Raster("before-eight-digit", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\"><rect width=\"24\" height=\"24\" fill=\"#77736cff\"/></svg>"))
                Check(old.GetPixel(48, 48).R < .01f, "control: raw eight-digit fill becomes black");

            var fixture = JsonNode.Parse("""
                {"ui_ability_styles":[{"id":"default","background":"#00000000","tint_alpha":0}],
                 "ui_ability_icons":[
                  {"id":"ink","layers":[{"path":"M2 2H22V22H2Z","fill":"ink"}]},
                  {"id":"tint","layers":[{"path":"M2 2H22V22H2Z","fill":"tint"}]}]}
                """)!;
            var samples = AbilityIcons.Load(kind => fixture[kind]?.AsArray().Select(n => n!.AsObject()) ?? Enumerable.Empty<JsonObject>());
            var tone = Color.FromHtml("#77736c");
            using (var opaque = Raster("after-dormant", samples.Svg(new AbilityFace("ink", "#ffffff", null, "#" + tone.ToHtml()))))
                CheckPixel(opaque, tone, "Godot ToHtml dormant tone retains visible RGB");
            using (var alpha = Raster("after-meta-alpha", samples.Svg(new AbilityFace("tint", "#12345680", null))))
                CheckPixel(alpha, Color.FromHtml("#12345680"), "authored eight-digit palette preserves alpha");
            using (var alphaTone = Raster("after-tone-alpha", samples.Svg(new AbilityFace("ink", "#ffffff", null, "#77736c80"))))
                CheckPixel(alphaTone, Color.FromHtml("#77736c80"), "translucent dormant tone preserves alpha");
            using (var transparent = Raster("after-transparent", samples.Svg(new AbilityFace("tint", "#12345600", null))))
                Check(transparent.GetPixel(48, 48).A == 0, "fully transparent palette remains transparent");

            // The two actual dormant faces staged by Mockup, in both shipped themes.
            foreach (var theme in raw["ui_themes"].Values)
            {
                var paint = Color.FromHtml(theme.Object["states"]!["dormant"]!.GetValue<string>());
                foreach (var (name, face) in new[]
                {
                    ("volley", icons.Glyph("volley")),
                    ("umbra-dash", icons.Seat("umbra_dash", "dash", null, null, new[] { "umbra" })),
                })
                {
                    var stem = theme.Object["id"]!.GetValue<string>() + "-" + name;
                    using var actual = Raster(stem, icons.Svg(face with { Tone = "#" + paint.ToHtml() }));
                    using var reference = Raster(null, icons.Svg(face with { Tone = "#" + paint.ToHtml(false) }));
                    Check(actual.GetData().SequenceEqual(reference.GetData()), stem + ": RGBA and RGB raster pixels identical");
                    var visible = 0;
                    for (var y = 0; y < actual.GetHeight(); y++)
                        for (var x = 0; x < actual.GetWidth(); x++)
                            if (Near(actual.GetPixel(x, y), paint)) visible++;
                    Check(visible > 8, stem + $": visible theme-painted pixels ({visible})");
                }
            }
            GD.Print($"ICON RASTER DONE {_passed}/{_passed} passed; CPU SVG pixels only, no graphical UI coverage");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PrintErr("ICON RASTER FAIL " + ex);
            GetTree().Quit(1);
        }
    }

    private Image Raster(string? name, string svg)
    {
        // Same explicit dimensions and scale conversion as AbilityArt.Draw.
        svg = svg.Contains("viewBox=") ? svg.Replace("viewBox=", "width=\"24\" height=\"24\" viewBox=", StringComparison.Ordinal) : svg;
        var image = new Image();
        var error = image.LoadSvgFromString(svg, 4);
        if (error != Error.Ok) { image.Dispose(); throw new InvalidOperationException("SVG raster: " + error); }
        if (name is not null)
        {
            File.WriteAllText(Path.Combine(_out, name + ".svg"), svg);
            if (image.SavePng(Path.Combine(_out, name + ".png")) != Error.Ok)
                throw new InvalidOperationException("Could not save raster evidence");
        }
        return image;
    }

    private static bool Near(Color a, Color b) => Math.Abs(a.R - b.R) < .012f && Math.Abs(a.G - b.G) < .012f &&
        Math.Abs(a.B - b.B) < .012f && Math.Abs(a.A - b.A) < .012f;

    private void CheckPixel(Image image, Color expected, string label)
    {
        var pixel = image.GetPixel(48, 48);
        Check(Near(pixel, expected), label + $": pixel {pixel.ToHtml()} expected {expected.ToHtml()}");
    }

    private void Check(bool passed, string label)
    {
        if (!passed) throw new InvalidOperationException(label);
        _passed++;
        GD.Print("ICON RASTER PASS " + label);
    }
}
