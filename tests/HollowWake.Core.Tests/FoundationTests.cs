using HollowWake.Core.Attributes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using Xunit;

namespace HollowWake.Core.Tests;

public class ExpressionTests
{
    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    [InlineData("-2 ^ 2", -4)]
    [InlineData("10 / 0", 0)]
    [InlineData("20% * 50", 10)]
    [InlineData("min(3, 1, 2)", 1)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("1 < 2 && 3 >= 3", 1)]
    [InlineData("1 > 2 || false", 0)]
    [InlineData("true ? 5 : 6", 5)]
    [InlineData("round(2.5)", 3)]
    [InlineData("diminish(100, 100)", 0.5)]
    public void EvaluatesConstants(string source, double expected)
    {
        Assert.Equal(expected, Expr.Parse(source).Evaluate(EmptyScope.Instance), 6);
    }

    [Fact]
    public void ReadsScopeAndFlags()
    {
        var scope = new DictScope().Set("level", 7).Set("life.max", 120).Flag("burning");
        Assert.Equal(180, Expr.Parse("40 + level * 10 + life.max * 0.5 + (has(\"burning\") ? 10 : 0)").Evaluate(scope));
        Assert.Equal(0, Expr.Parse("has(\"frozen\")").Evaluate(scope));
    }

    [Fact]
    public void CollectsIdentifiersAndFlags()
    {
        var e = Expr.Parse("might * 2 + grace + has(\"stationary\")");
        Assert.Equal(new[] { "might", "grace" }, e.Identifiers);
        Assert.Equal(new[] { "stationary" }, e.Flags);
    }

    [Fact]
    public void RejectsBadSyntax()
    {
        Assert.False(Expr.TryParse("1 +", out _, out var error));
        Assert.NotNull(error);
        Assert.Throws<ExprParseException>(() => Expr.Parse("unknownfn(1)"));
        Assert.Throws<ExprEvalException>(() => Expr.Parse("nothing").Evaluate(EmptyScope.Instance));
    }

    [Fact]
    public void MountedScopesResolveDottedNames()
    {
        var target = new DictScope().Set("life", 50).Flag("burning");
        var scope = new ScopeOverlay().Mount("target", target).Set("amount", 10);
        Assert.Equal(60, Expr.Parse("target.life + amount").Evaluate(scope));
        Assert.True(Expr.Parse("has(\"target.burning\")").IsTrue(scope));
    }
}

public class TagTests
{
    [Fact]
    public void HierarchyAndSetOperations()
    {
        var tags = TagSet.Of("weapon.sword", "Melee", "attack");
        Assert.True(tags.Has("weapon"));
        Assert.True(tags.Has("weapon.sword"));
        Assert.False(tags.Has("weapon.axe"));
        Assert.True(tags.Has("melee"));
        Assert.True(tags.HasAll(TagSet.Of("weapon", "attack")));
        Assert.False(tags.HasAll(TagSet.Of("weapon", "spell")));
        Assert.Equal(TagSet.Of("attack", "melee", "weapon.sword"), tags);
        Assert.Equal(3, tags.Count);
        Assert.Equal(4, tags.Union(TagSet.Of("fire")).Count);
    }
}

public class RngTests
{
    [Fact]
    public void IsDeterministicAndForksIndependently()
    {
        var a = new Rng("seed");
        var b = new Rng("seed");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
        var fork1 = new Rng("seed").Fork("x");
        var fork2 = new Rng("seed").Fork("x");
        var fork3 = new Rng("seed").Fork("y");
        Assert.Equal(fork1.NextUInt64(), fork2.NextUInt64());
        Assert.NotEqual(fork1.NextUInt64(), fork3.NextUInt64());
    }

    [Fact]
    public void WeightedPickRespectsWeights()
    {
        var rng = new Rng(42);
        var counts = new int[3];
        for (var i = 0; i < 6000; i++)
        {
            counts[rng.PickWeighted(new[] { 1.0, 2.0, 3.0 })]++;
        }
        Assert.InRange(counts[0], 700, 1300);
        Assert.InRange(counts[2], 2600, 3400);
        Assert.Equal(-1, rng.PickWeighted(new[] { 0.0, 0.0 }));
    }
}

public class AttributeTests
{
    private static AttributeCatalog Catalog() => new(new[]
    {
        new AttributeDef { Id = "might", Base = 10 },
        new AttributeDef { Id = "life.max", Formula = Expr.Parse("40 + level * 10 + might * 4"), Min = 1 },
        new AttributeDef { Id = "life.regen", Formula = Expr.Parse("1 + might * 0.1") },
        new AttributeDef { Id = "thorns", Base = 0 },
        new AttributeDef { Id = "damage", Base = 0 },
        new AttributeDef { Id = "crit.chance", Base = 0.05, Max = 1 },
    });

    [Fact]
    public void FoldsFlatIncreasedMoreOverride()
    {
        var set = new AttributeSet(Catalog());
        var scope = new DictScope().Set("level", 5);
        var owner = new ScopeOverlay(scope);
        // life.max = 40 + 50 + 40 = 130
        Assert.Equal(130, set.Get("life.max", Owner(set, scope)));
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Flat, Value = 20, Source = "a" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Increased, Value = 0.2, Source = "b" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Increased, Value = 0.3, Source = "c" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.More, Value = 0.5, Source = "d" });
        // (130 + 20) * 1.5 * 1.5 = 337.5
        Assert.Equal(337.5, set.Get("life.max", Owner(set, scope)), 6);
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Override, Value = 1, Source = "e" });
        Assert.Equal(1, set.Get("life.max", Owner(set, scope)));
        Assert.Equal(1, set.RemoveSource("e"));
        Assert.Equal(337.5, set.Get("life.max", Owner(set, scope)), 6);
        _ = owner;
    }

    [Fact]
    public void ScopedModifiersApplyOnlyWithContext()
    {
        var set = new AttributeSet(Catalog());
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.5, Scope = TagSet.Of("ember", "spell") });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.25 });
        var scope = EmptyScope.Instance;
        Assert.Equal(125, set.Apply("damage", 100, scope, TagSet.Of("ember")));
        Assert.Equal(175, set.Apply("damage", 100, scope, TagSet.Of("ember", "spell", "projectile")));
        Assert.Equal(1.75, set.Multiplier("damage", scope, TagSet.Of("ember", "spell")), 6);
    }

    [Fact]
    public void ExtraModifiersJoinTheSameFold()
    {
        var set = new AttributeSet(Catalog());
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.3 });
        var extra = new[] { new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.2 }, new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 0.5 } };
        // increased sums additively: 1.5; more multiplies: 1.5 -> 100 * 1.5 * 1.5 = 225
        Assert.Equal(225, set.Apply("damage", 100, EmptyScope.Instance, TagSet.Empty, extra), 6);
    }

    [Fact]
    public void LinksAreSingleHopAndJoinBase()
    {
        var set = new AttributeSet(Catalog());
        var scope = new DictScope().Set("level", 1);
        // thorns gains 50% of life.regen (= 1 + 10*0.1 = 2) -> 1; a link from thorns back to regen does not compound.
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Link, From = "life.regen", Value = 0.5 });
        set.Add(new Modifier { Attribute = "life.regen", Form = ModifierForm.Link, From = "thorns", Value = 1.0 });
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Increased, Value = 1.0 });
        Assert.Equal(2, set.Get("thorns", Owner(set, scope)), 6);
    }

    [Fact]
    public void ConditionsAndGaugesGate()
    {
        var set = new AttributeSet(Catalog());
        var dict = new DictScope().Set("level", 1).Set("status.poison.stacks", 3);
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.1, Gauge = "status.poison.stacks" });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 1.0, Condition = Expr.Parse("has(\"low_life\")") });
        Assert.Equal(130, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        dict.Flag("low_life");
        set.Touch();
        Assert.Equal(260, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 0.5, Gauge = "status.poison.stacks", GaugeAt = 5 });
        Assert.Equal(0.05, set.Get("crit.chance", dict), 6);
        dict.Set("status.poison.stacks", 5);
        set.Touch();
        Assert.Equal(0.55, set.Get("crit.chance", dict), 6);
    }

    [Fact]
    public void ClampsAndDetectsCycles()
    {
        var set = new AttributeSet(Catalog());
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 5 });
        Assert.Equal(1, set.Get("crit.chance", EmptyScope.Instance));
        var cyclic = new AttributeSet(new AttributeCatalog(new[] { new AttributeDef { Id = "a", Formula = Expr.Parse("b + 1") }, new AttributeDef { Id = "b", Formula = Expr.Parse("a + 1") } }));
        var owner = new AttributeOwnerScope(cyclic);
        Assert.Throws<AttributeCycleException>(() => cyclic.Get("a", owner));
    }

    private static IExprScope Owner(AttributeSet set, IExprScope extra) => new AttributeOwnerScope(set, extra);

    /// <summary>Resolves names through the attribute set first, then an extra scope. Mirrors the Stats component.</summary>
    private sealed class AttributeOwnerScope : IExprScope
    {
        private readonly AttributeSet _set;
        private readonly IExprScope? _extra;

        public AttributeOwnerScope(AttributeSet set, IExprScope? extra = null)
        {
            _set = set;
            _extra = extra;
        }

        public bool TryResolve(string name, out double value)
        {
            if (_extra is not null && _extra.TryResolve(name, out value))
            {
                return true;
            }
            if (_set.Catalog.Contains(name) || _set.HasModifiers(name))
            {
                value = _set.Get(name, this);
                return true;
            }
            value = 0;
            return false;
        }

        public bool HasFlag(string flag) => _extra?.HasFlag(flag) ?? false;
        public double NextRandom() => 0.5;
    }
}

public class ContentTests
{
    [Fact]
    public void ParsesYamlIntoTypedDefinitionsWithLayersAndPatches()
    {
        var report = new ValidationReport();
        var baseDocs = ContentLoader.ParseText("""
            kind: attributes
            entries:
              - { id: might, base: 10, name: Might }
              - { id: life.max, formula: "40 + might * 4", min: 1 }
            """, "base.yaml", report);
        var modDocs = ContentLoader.ParseText("""
            kind: attributes
            entries:
              - { id: might, patch: true, base: 12 }
              - { id: life.max, remove: true }
              - { id: grace, base: 10 }
            """, "mod.yaml", report);
        var raw = new Dictionary<string, Dictionary<string, RawDefinition>>();
        foreach (var doc in baseDocs.Concat(modDocs))
        {
            // Apply through the public loader path by writing temp files.
        }
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-content-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "base"));
        Directory.CreateDirectory(Path.Combine(dir, "mod"));
        File.WriteAllText(Path.Combine(dir, "base", "a.yaml"), """
            kind: attributes
            entries:
              - { id: might, base: 10, name: Might }
              - { id: life.max, formula: "40 + might * 4", min: 1 }
            """);
        File.WriteAllText(Path.Combine(dir, "mod", "a.yaml"), """
            kind: attributes
            entries:
              - { id: might, patch: true, base: 12 }
              - { id: life.max, remove: true }
              - { id: grace, base: 10, bogus_key: 1 }
            """);
        try
        {
            var registry = ContentRegistry.Load(new[] { Path.Combine(dir, "base"), Path.Combine(dir, "mod") }, out var loadReport);
            Assert.False(loadReport.HasErrors, loadReport.ToString());
            Assert.Equal(12, registry.Require<AttributeDef>("might").Base);
            Assert.Equal("Might", registry.Require<AttributeDef>("might").Name);
            Assert.False(registry.Has<AttributeDef>("life.max"));
            Assert.True(registry.Has<AttributeDef>("grace"));
            Assert.Contains(loadReport.Warnings, w => w.Message.Contains("bogus_key"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        _ = raw;
    }

    [Fact]
    public void YamlScalarsKeepTheirTypes()
    {
        var docs = YamlToJson.ParseDocuments("a: 1\nb: 1.5\nc: \"20\"\nd: true\ne: ~\nf: 20%\n");
        var obj = Assert.IsType<System.Text.Json.Nodes.JsonObject>(docs[0]);
        Assert.Equal(1L, obj["a"]!.GetValue<long>());
        Assert.Equal(1.5, obj["b"]!.GetValue<double>());
        Assert.Equal("20", obj["c"]!.GetValue<string>());
        Assert.True(obj["d"]!.GetValue<bool>());
        Assert.Null(obj["e"]);
        Assert.Equal("20%", obj["f"]!.GetValue<string>());
    }
}
