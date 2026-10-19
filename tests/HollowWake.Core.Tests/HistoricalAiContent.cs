using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;

namespace HollowWake.Core.Tests;

/// <summary>
/// The pre-intent movement recordings used direct-target Strike, before positional melee and
/// anticipation changed when a hunter stops walking to attack. Freeze that fixture input so these
/// recordings still isolate movement dispatch; current combat uses AiApproach/PositionalRelease tests.
/// </summary>
internal static class HistoricalAiContent
{
    private static readonly Lazy<ContentRegistry> Content = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _, strict: true);
        registry.With(CoreContent.Registry.Require<FactionDef>("red"));
        registry.With(CoreContent.Registry.Require<FactionDef>("blue"));
        var strike = registry.Get<FormDef>("strike")!;
        return registry.With(strike with
        {
            ReleaseCheck = new ReleaseCheckSpec { Policy = "body" },
            Attributes = new(strike.Attributes) { ["ai.anticipation"] = 0 },
            Effects = new()
            {
                ContentJson.Bind<TypedSpec>(JsonNode.Parse("""
                    {"type":"damage","amount":"lerp(weapon.damage.min, weapon.damage.max, roll) * weave.damage.effectiveness * attack.power","threads":"$threads","tags":["hit"]}
                    """)!),
            },
        });
    });

    public static ContentRegistry Registry => Content.Value;
}
