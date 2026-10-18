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

    [Fact]
    public void FunctionsTakeManyArguments()
    {
        static double Eval(string source) => Expr.Parse(source).Evaluate(EmptyScope.Instance);
        // Nine arguments: one more than the evaluator keeps on the stack.
        const string nine = "1, 9, 2, 8, 3, 7, 4, 6, 5";
        Assert.Equal(1, Eval($"min({nine})"));
        Assert.Equal(9, Eval($"max({nine})"));
        Assert.Equal(45, Eval($"sum({nine})"));
        Assert.Equal(5, Eval($"avg({nine})"));
        Assert.Equal(8, Eval("max(1, 2, 3, 4, 5, 6, 7, 8)"));
        var all = string.Join(", ", Enumerable.Range(1, 64));
        Assert.Equal(2080, Eval($"sum({all})"));
        Assert.Equal(64, Eval($"max({all})"));
        Assert.Equal(32.5, Eval($"avg({all})"));
        Assert.Throws<ExprParseException>(() => Expr.Parse($"sum({all}, 65)"));
        Assert.Throws<ExprParseException>(() => Expr.Parse("clamp(1, 2)"));
        // Nested calls each keep their own arguments, long lists included.
        Assert.Equal(10, Eval("max(min(5, 3, 9, 1, 7, 2, 8, 6, 4), sum(1, 1, 1, 1, 1, 1, 1, 1, 1, 1), avg(2, 4))"));
        Assert.Equal(18, Eval("sum(1, 1, 1, 1, 1, 1, 1, 1, 1, max(1, 2, 3, 4, 5, 6, 7, 8, 9, 8, 7))"));
        // Arguments evaluate left to right, so rand() draws keep their order.
        var expected = new Rng(5);
        var first = expected.NextDouble();
        var second = expected.NextDouble();
        Assert.Equal(first + second * 1000, Expr.Parse("sum(rand(), rand() * 1000, 0, 0, 0, 0, 0, 0, 0)").Evaluate(new DictScope(new Rng(5))));
    }

    [Fact]
    public void ListFunctionsMatchLinqExactly()
    {
        // min, max, sum and avg once ran through LINQ; they must keep its results bit for bit, NaN and signed zeros included.
        var specials = new[] { 0.0, -0.0, 1.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.1, 1e300, -1e-300 };
        var rng = new Rng(11);
        for (var trial = 0; trial < 400; trial++)
        {
            var n = 1 + (int)(rng.NextDouble() * 12);
            var values = new double[n];
            var scope = new DictScope();
            var names = new string[n];
            for (var i = 0; i < n; i++)
            {
                values[i] = rng.NextDouble() < 0.5 ? specials[(int)(rng.NextDouble() * specials.Length)] : (rng.NextDouble() - 0.5) * 100;
                names[i] = "a" + i;
                scope.Set(names[i], values[i]);
            }
            var args = string.Join(", ", names);
            AssertSameDouble(values.Min(), Expr.Parse($"min({args})").Evaluate(scope));
            AssertSameDouble(values.Max(), Expr.Parse($"max({args})").Evaluate(scope));
            AssertSameDouble(values.Sum(), Expr.Parse($"sum({args})").Evaluate(scope));
            AssertSameDouble(values.Average(), Expr.Parse($"avg({args})").Evaluate(scope));
        }
        // Ties keep the earlier value.
        AssertSameDouble(0.0, Expr.Parse("min(0, -0)").Evaluate(EmptyScope.Instance));
        AssertSameDouble(-0.0, Expr.Parse("min(-0, 0)").Evaluate(EmptyScope.Instance));
        AssertSameDouble(-0.0, Expr.Parse("max(-0, 0)").Evaluate(EmptyScope.Instance));
        Assert.True(double.IsNaN(Expr.Parse("min(1, pow(-8, 0.5))").Evaluate(EmptyScope.Instance)));
        Assert.Equal(1, Expr.Parse("max(pow(-8, 0.5), 1)").Evaluate(EmptyScope.Instance));
    }

    private static void AssertSameDouble(double expected, double actual)
    {
        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN(actual), $"expected NaN, got {actual}");
            return;
        }
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }

    [Fact]
    public void EvaluationAllocatesNothing()
    {
        var scope = new DictScope(new Rng(1)).Set("a", 1).Set("b", 2).Flag("stationary");
        var expr = Expr.Parse("max(a, b, 3, 4, 5, 6, 7, 8) + min(a, b) + lerp(a, b, rand()) + (has(\"stationary\") ? 1 : 0) + clamp(a / max(1, b), 0, 1) + sum(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");
        var total = 0.0;
        SteadyState.AssertAllocationFree(() => total += expr.Evaluate(scope), callsPerRound: 1000);
        Assert.True(total > 0);
    }

    [Fact]
    public void DictScopeKeepsEveryNameAndTheLatestValue()
    {
        var scope = new DictScope();
        for (var i = 0; i < 20; i++)
        {
            scope.Set("v" + i, i);
        }
        scope.Set("v3", 33).Set("v15", 150);
        for (var i = 0; i < 20; i++)
        {
            Assert.True(scope.TryResolve("v" + i, out var v));
            Assert.Equal(i == 3 ? 33 : i == 15 ? 150 : i, v);
        }
        Assert.False(scope.TryResolve("v20", out var missing));
        Assert.Equal(0, missing);
        Assert.False(scope.HasFlag("x"));
        scope.Flag("x");
        Assert.True(scope.HasFlag("x"));
        scope.Flag("x", false);
        Assert.False(scope.HasFlag("x"));
        Assert.Throws<ArgumentNullException>(() => scope.Set(null!, 1));
    }

    [Fact]
    public void OverlaysShadowMountAndFallBack()
    {
        var parent = new DictScope().Set("life", 80).Set("target.life", 1).Set("source.life", 7).Flag("burning");
        var target = new DictScope().Set("life", 50).Flag("frozen");
        var overlay = new ScopeOverlay(parent).Set("life", 10).Mount("target", target);
        Assert.Equal(10, Expr.Parse("life").Evaluate(overlay));
        // A mounted prefix answers alone; other prefixes and plain names fall through to the parent.
        Assert.Equal(50, Expr.Parse("target.life").Evaluate(overlay));
        Assert.False(overlay.TryResolve("target.mana", out _));
        Assert.Equal(7, Expr.Parse("source.life").Evaluate(overlay));
        Assert.True(overlay.HasFlag("target.frozen"));
        Assert.False(overlay.HasFlag("target.burning"));
        Assert.True(overlay.HasFlag("burning"));
        // Mounting again under a prefix replaces the scope.
        overlay.Mount("target", new DictScope().Set("life", 5));
        Assert.Equal(5, Expr.Parse("target.life").Evaluate(overlay));
        Assert.Equal(15, Expr.Parse("target.life + life").Evaluate(overlay));
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
    public void ScopesMatchHierarchicallyAcrossContexts()
    {
        var set = new AttributeSet(Catalog());
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 1, Scope = TagSet.Of("weapon") });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 10, Scope = TagSet.Of("weapon.sword") });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 100, Scope = TagSet.Of("weapon", "fire") });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 1000, Scope = TagSet.Of("weapon") });
        var scope = EmptyScope.Instance;
        var sword = TagSet.Of("weapon.sword", "attack");
        var axe = TagSet.Of("weapon.axe", "fire");
        var bare = TagSet.Of("weapon");
        // Alternating contexts replace whatever the set remembers about the last one.
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(1011, set.Apply("damage", 0, scope, sword));
            Assert.Equal(1101, set.Apply("damage", 0, scope, axe));
            Assert.Equal(1001, set.Apply("damage", 0, scope, bare));
            Assert.Equal(0, set.Apply("damage", 0, scope, TagSet.Empty));
        }
        // Past 64 distinct scopes every modifier still matches on its own.
        for (var i = 0; i < 70; i++)
        {
            set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = i, Scope = TagSet.Of("t" + i) });
        }
        Assert.Equal(1011 + 3 + 65, set.Apply("damage", 0, scope, sword.With("t3", "t65")));
        Assert.Equal(1001, set.Apply("damage", 0, scope, bare));
        Assert.Equal(0, set.Apply("damage", 0, scope, TagSet.Empty));
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

    [Fact]
    public void LinksStaySingleHopWithExtras()
    {
        var set = new AttributeSet(Catalog());
        var owner = Owner(set, new DictScope().Set("level", 1));
        // life.regen = 1 + might(10) * 0.1 = 2. thorns gains half of it; the link back from thorns must not compound.
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Link, From = "life.regen", Value = 0.5, Source = "link" });
        set.Add(new Modifier { Attribute = "life.regen", Form = ModifierForm.Link, From = "thorns", Value = 1.0, Source = "back" });
        Assert.Equal(1, set.Get("thorns", owner), 6);
        Assert.Equal(2, set.Get("life.regen", owner), 6);
        // Extras join the source's fold too (+2 regen, read with links off = 4), and may carry links of their own.
        var extra = new[]
        {
            new Modifier { Attribute = "life.regen", Form = ModifierForm.Flat, Value = 2, Source = "x.regen" },
            new Modifier { Attribute = "thorns", Form = ModifierForm.Increased, Value = 1.0, Source = "x.inc" },
            new Modifier { Attribute = "thorns", Form = ModifierForm.Link, From = "life.regen", Value = 0.25, Source = "x.link" },
        };
        // (0.5 * 4 + 0.25 * 4) * (1 + 1) = 6
        Assert.Equal(6, set.Get("thorns", owner, TagSet.Empty, extra), 6);
        var breakdown = set.Explain("thorns", owner, null, extra);
        Assert.Equal(3, breakdown.Flat, 6);
        Assert.Equal(1, breakdown.Increased, 6);
        Assert.Equal(6, breakdown.Result, 6);
        Assert.Equal(new[] { "link", "x.inc", "x.link" }, breakdown.Applied.Select(m => m.Source));
        // Links to an attribute nobody declared read zero; a link with no source is ignored.
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Link, From = "nothing.here", Value = 3 });
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Link, Value = 3 });
        Assert.Equal(1, set.Get("thorns", owner), 6);
        // The cached query and the uncached one agree once no extras are involved.
        Assert.Equal(set.Get("thorns", owner), set.Apply("thorns", 0, owner, TagSet.Empty), 6);
    }

    [Fact]
    public void GaugeGatingEdges()
    {
        var set = new AttributeSet(Catalog());
        var dict = new DictScope().Set("level", 2);
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 10, Gauge = "charge.rage", GaugeAt = 3 });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 2, Gauge = "status.poison.stacks" });
        // An unknown gauge reads zero: below the gate, and inert as a scale.
        Assert.Equal(100, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        dict.Set("charge.rage", 2.999);
        Assert.Equal(100, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // At the threshold the gate opens with the full value, and above it the value is not scaled.
        dict.Set("charge.rage", 3);
        Assert.Equal(110, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        dict.Set("charge.rage", 7);
        Assert.Equal(110, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // A scaling gauge multiplies by whatever it reads, negative included; exactly zero makes it inert.
        dict.Set("status.poison.stacks", -1.5);
        Assert.Equal(107, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        dict.Set("status.poison.stacks", 0);
        Assert.Equal(110, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // gauge_at 0 opens even for a gauge that reads nothing.
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 1, Gauge = "charge.missing", GaugeAt = 0 });
        Assert.Equal(111, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // The condition gates before the gauge does.
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 100, Gauge = "charge.rage", GaugeAt = 1, Condition = Expr.Parse("has(\"enraged\")") });
        Assert.Equal(111, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        dict.Flag("enraged");
        Assert.Equal(211, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // A value expression is scaled by its gauge like a constant.
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, ValueExpr = Expr.Parse("level * 0.1"), Gauge = "status.poison.stacks" });
        dict.Set("status.poison.stacks", 3);
        // (100 + 10 + 2 * 3 + 1 + 100) * (1 + 0.2 * 3) = 217 * 1.6
        Assert.Equal(217 * 1.6, set.Apply("damage", 100, dict, TagSet.Empty), 6);
        // Cached reads see gauge changes once the set is touched, as the Stats component does.
        Assert.Equal(0.05, set.Get("crit.chance", dict), 6);
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 0.5, Gauge = "charge.rage", GaugeAt = 8 });
        Assert.Equal(0.05, set.Get("crit.chance", dict), 6);
        dict.Set("charge.rage", 8);
        Assert.Equal(0.05, set.Get("crit.chance", dict), 6);
        set.Touch();
        Assert.Equal(0.55, set.Get("crit.chance", dict), 6);
    }

    [Fact]
    public void OverridePriorityTies()
    {
        var set = new AttributeSet(Catalog());
        var scope = EmptyScope.Instance;
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.3, Priority = 1, Source = "a" });
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.4, Priority = 1, Source = "b" });
        // Equal priorities: the later override wins.
        Assert.Equal(0.4, set.Get("crit.chance", scope), 6);
        // A lower priority added later loses; a higher one wins until removed.
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.2, Priority = 0, Source = "c" });
        Assert.Equal(0.4, set.Get("crit.chance", scope), 6);
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.9, Priority = 2, Source = "d" });
        Assert.Equal(0.9, set.Get("crit.chance", scope), 6);
        Assert.Equal(1, set.RemoveSource("d"));
        Assert.Equal(0.4, set.Get("crit.chance", scope), 6);
        // Extras fold after the owner's modifiers, so an equal-priority extra takes the tie.
        var extra = new[] { new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.6, Priority = 1, Source = "x" } };
        Assert.Equal(0.6, set.Get("crit.chance", scope, TagSet.Empty, extra), 6);
        var explained = set.Explain("crit.chance", scope, null, extra);
        Assert.Equal(0.6, explained.Override);
        Assert.Equal(new[] { "a", "b", "c", "x" }, explained.Applied.Select(m => m.Source));
        // An override replaces flat, increased and more, but bounds still apply.
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Flat, Value = 0.25 });
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.More, Value = 1.0 });
        Assert.Equal(0.4, set.Get("crit.chance", scope), 6);
        set.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 5, Priority = 1, Source = "e" });
        Assert.Equal(1, set.Get("crit.chance", scope), 6);
        // The first override lands whatever its priority, and ties at the minimum still go to the later one.
        var low = new AttributeSet(Catalog());
        low.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.25, Priority = int.MinValue });
        Assert.Equal(0.25, low.Get("crit.chance", scope), 6);
        low.Add(new Modifier { Attribute = "crit.chance", Form = ModifierForm.Override, Value = 0.75, Priority = int.MinValue });
        Assert.Equal(0.75, low.Get("crit.chance", scope), 6);
        Assert.Null(new AttributeSet(Catalog()).Explain("crit.chance", scope).Override);
    }

    [Fact]
    public void ExplainAccountsForTheFold()
    {
        var set = new AttributeSet(Catalog());
        var owner = Owner(set, new DictScope().Set("level", 5).Set("status.stacks", 2));
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Flat, Value = 20, Source = "a" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Increased, Value = 0.2, Source = "b" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Increased, Value = 0.3, Scope = TagSet.Of("ember"), Source = "c" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.More, Value = 0.5, Condition = Expr.Parse("has(\"low_life\")"), Source = "d" });
        set.Add(new Modifier { Attribute = "life.max", Form = ModifierForm.Flat, Value = 5, Gauge = "status.stacks", Source = "e" });
        var extra = new[]
        {
            new Modifier { Attribute = "life.max", Form = ModifierForm.More, Value = 0.1, Source = "x" },
            new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 99, Source = "y" },
        };
        var context = TagSet.Of("ember", "spell");
        var b = set.Explain("life.max", owner, context, extra);
        Assert.Equal("life.max", b.Attribute);
        Assert.Equal(130, b.Base, 6);       // 40 + 5 * 10 + 10 * 4
        Assert.Equal(30, b.Flat, 6);        // 20 + 5 * 2 stacks
        Assert.Equal(0.5, b.Increased, 6);  // 0.2 + 0.3 for ember
        Assert.Equal(1.1, b.More, 6);       // the low_life more is off; the extra's is on
        Assert.Null(b.Override);
        Assert.Equal(160 * 1.5 * 1.1, b.Result, 6);
        Assert.Equal(new[] { "a", "b", "c", "e", "x" }, b.Applied.Select(m => m.Source));
        Assert.Equal(b.Result, set.Get("life.max", owner, context, extra), 6);
        // Without the context the scoped modifier drops out, and without extras so does theirs.
        var plain = set.Explain("life.max", owner);
        Assert.Equal(new[] { "a", "b", "e" }, plain.Applied.Select(m => m.Source));
        Assert.Equal(160 * 1.2, plain.Result, 6);
        Assert.Equal(plain.Result, set.Get("life.max", owner), 6);
        // Bounds and rounding apply to the result, not the parts.
        var bounded = new AttributeSet(new AttributeCatalog(new[] { new AttributeDef { Id = "speed", Base = 1, Max = 2, Decimals = 1 } }));
        bounded.Add(new Modifier { Attribute = "speed", Form = ModifierForm.Increased, Value = 0.333 });
        Assert.Equal(1.3, bounded.Explain("speed", EmptyScope.Instance).Result, 9);
        Assert.Equal(0.333, bounded.Explain("speed", EmptyScope.Instance).Increased, 9);
        bounded.Add(new Modifier { Attribute = "speed", Form = ModifierForm.Flat, Value = 5 });
        Assert.Equal(2, bounded.Explain("speed", EmptyScope.Instance).Result, 9);
    }

    [Fact]
    public void CachesPerContextUntilTouched()
    {
        var set = new AttributeSet(Catalog());
        var dict = new DictScope().Set("rage", 1);
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, ValueExpr = Expr.Parse("rage"), Source = "rage" });
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 10, Scope = TagSet.Of("ember"), Source = "ember" });
        Assert.Equal(1, set.Get("damage", dict));
        Assert.Equal(11, set.Get("damage", dict, TagSet.Of("ember", "spell")));
        dict.Set("rage", 5);
        // Cached until touched; an equal context built separately shares the cached value.
        Assert.Equal(1, set.Get("damage", dict));
        Assert.Equal(11, set.Get("damage", dict, TagSet.Of("spell", "ember")));
        // Folds with extras or an explicit base, and Apply, are never cached.
        Assert.Equal(15, set.Get("damage", dict, TagSet.Of("ember"), Array.Empty<Modifier>(), baseValue: 0));
        Assert.Equal(105, set.Apply("damage", 100, dict, TagSet.Empty));
        set.Touch();
        Assert.Equal(5, set.Get("damage", dict));
        Assert.Equal(15, set.Get("damage", dict, TagSet.Of("ember")));
        // Structural changes invalidate too.
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Increased, Value = 1, Scope = TagSet.Of("spell"), Source = "spell" });
        Assert.Equal(30, set.Get("damage", dict, TagSet.Of("ember", "spell")));
        Assert.Equal(5, set.Get("damage", dict));
        Assert.Equal(1, set.RemoveSource("spell"));
        Assert.True(set.HasModifiers("damage"));
        Assert.Equal(15, set.Get("damage", dict, TagSet.Of("ember", "spell")));
        Assert.Equal(1, set.RemoveSource("rage"));
        Assert.Equal(1, set.RemoveSource("ember"));
        Assert.Equal(0, set.RemoveSource("ember"));
        Assert.False(set.HasModifiers("damage"));
        Assert.Equal(0, set.Get("damage", dict, TagSet.Of("ember", "spell")));
        Assert.Empty(set.ModifiersFor("damage"));
        Assert.Empty(set.ModifiedAttributes);
    }

    [Fact]
    public void ConditionsOnTheirOwnAttributeAreCycles()
    {
        var set = new AttributeSet(Catalog());
        var owner = Owner(set, new DictScope().Set("level", 1));
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Flat, Value = 1, Condition = Expr.Parse("damage > 0"), Source = "loop" });
        Assert.Throws<AttributeCycleException>(() => set.Get("damage", owner));
        set.Add(new Modifier { Attribute = "thorns", Form = ModifierForm.Flat, ValueExpr = Expr.Parse("thorns + 1"), Source = "loop" });
        Assert.Throws<AttributeCycleException>(() => set.Apply("thorns", 0, owner, TagSet.Empty));
        // The guard is released after a failure: other attributes still resolve, and so do these once the loop is gone.
        Assert.Equal(10, set.Get("might", owner));
        Assert.Equal(2, set.RemoveSource("loop"));
        Assert.Equal(0, set.Get("damage", owner));
        Assert.Equal(0, set.Get("thorns", owner));
    }

    [Fact]
    public void ModifierBookkeeping()
    {
        var set = new AttributeSet(Catalog());
        var a = new Modifier { Attribute = "damage", Value = 1, Source = "s1" };
        var b = new Modifier { Attribute = "life.max", Value = 2, Source = "s2" };
        var c = new Modifier { Attribute = "damage", Value = 3, Source = "s2" };
        // Queries and base overrides do not change the order attributes report in.
        set.Get("thorns", EmptyScope.Instance);
        set.Add(a);
        set.Add(b);
        set.Add(c);
        set.SetBase("thorns", 4);
        Assert.Equal(new[] { "damage", "life.max" }, set.ModifiedAttributes);
        Assert.Equal(new[] { a, c, b }, set.AllModifiers);
        Assert.Equal(new[] { a, c }, set.ModifiersFor("damage"));
        Assert.Empty(set.ModifiersFor("thorns"));
        Assert.True(set.HasSource("s2"));
        // Remove takes the first equal modifier; records compare by value.
        Assert.True(set.Remove(new Modifier { Attribute = "damage", Value = 3, Source = "s2" }));
        Assert.False(set.Remove(c));
        Assert.Equal(new[] { a }, set.ModifiersFor("damage"));
        Assert.Equal(1, set.RemoveSource("s2"));
        Assert.Equal(1, set.RemoveSource("s1"));
        Assert.False(set.HasSource("s2"));
        Assert.Empty(set.ModifiedAttributes);
        Assert.Equal(4, set.Get("thorns", EmptyScope.Instance));
        Assert.True(set.ClearBase("thorns"));
        Assert.False(set.ClearBase("thorns"));
        Assert.Equal(0, set.Get("thorns", EmptyScope.Instance));
        set.SetBase("thorns", 6);
        set.Add(b);
        var version = set.Version;
        set.Clear();
        Assert.True(set.Version > version);
        Assert.Empty(set.AllModifiers);
        Assert.False(set.TryGetBaseOverride("thorns", out _));
        Assert.Equal(0, set.Get("thorns", EmptyScope.Instance));
        set.Add(a);
        Assert.Equal(new[] { "damage" }, set.ModifiedAttributes);
        Assert.Equal(101, set.Apply("damage", 100, EmptyScope.Instance, TagSet.Empty));
    }

    [Fact]
    public void FoldAllocatesNothing()
    {
        var set = new AttributeSet(Catalog());
        var dict = new DictScope().Set("level", 3).Set("life_pct", 0.8).Set("status.poison.stacks", 2);
        var tags = new[] { TagSet.Of("ember", "spell"), TagSet.Of("attack", "melee") };
        for (var i = 0; i < 24; i++)
        {
            set.Add(new Modifier
            {
                Attribute = "damage",
                Form = (ModifierForm)(i % 3),
                Value = 0.05 * (i + 1),
                Scope = i % 2 == 0 ? tags[i % 4 / 2] : TagSet.Empty,
                Condition = i % 5 == 0 ? Expr.Parse("life_pct > 0.5") : null,
                Gauge = i % 7 == 0 ? "status.poison.stacks" : null,
            });
        }
        set.Add(new Modifier { Attribute = "damage", Form = ModifierForm.Link, From = "might", Value = 0.1 });
        var extra = new List<Modifier>
        {
            new() { Attribute = "damage", Form = ModifierForm.More, Value = 0.3 },
            new() { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.2, Scope = TagSet.Of("spell") },
        };
        var total = 0.0;
        var call = 0;
        SteadyState.AssertAllocationFree(() =>
        {
            var t = tags[call++ % 2];
            total += set.Apply("damage", 100, dict, t, extra);
            total += set.Get("damage", dict, t, extra);
        }, callsPerRound: 1000);
        Assert.True(total > 0);
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
