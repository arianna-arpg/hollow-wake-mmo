using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using static HollowWake.Core.Weaves.AxisUnits;

namespace HollowWake.Core.Weaves.Axes;

/// <summary>
/// The summons family: who acts after the press besides the caster. The parameters <see cref="AxisBindings"/>
/// hands to this reader name the bodies an effect brings (a summon's, a deploy's or a spawn's pattern, a
/// weighted pool) and the weave a proxy casts for its owner; each is placed on its binding's dimension under
/// its binding's axis (<c>agency.summon</c>, <c>agency.minion_cast</c>), beside the counts, lifetimes and caps
/// the parameter walk places.
/// </summary>
internal static class SummonAxes
{
    public const string Family = "summons";

    public static void Read(AxisReading r)
    {
        foreach (var node in r.Nodes)
        {
            foreach (var binding in AxisBindings.For(node.Type))
            {
                if (binding.ReadBy != Family || binding.Dimension is not { } dimension || node.Node[binding.Parameter] is not { } json)
                {
                    continue;
                }
                var word = json is JsonArray pool && pool.All(e => e is JsonObject)
                    ? "pool: " + string.Join(",", pool.OfType<JsonObject>().Select(e => JsonNum.StringOf(e["pattern"])).Where(p => p is not null))
                    : r.Words(json);
                if (string.IsNullOrEmpty(word))
                {
                    continue;
                }
                var who = node.Provenance(binding.Parameter);
                r.Add(new AxisValue
                {
                    Dimension = dimension,
                    Name = AxisValue.DimensionName(dimension) + "." + binding.Axis,
                    Word = word,
                    Unit = Word,
                    Source = who?.Source ?? AxisSource.Literal,
                    Path = $"{node.Path}.{binding.Parameter}",
                    Effect = node.Type,
                    Parameter = binding.Parameter,
                    Detail = who?.By,
                });
            }
        }
    }
}
