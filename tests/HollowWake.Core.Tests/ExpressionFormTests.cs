using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The randi and curve forms of expressions.</summary>
public class ExpressionFormTests
{
    [Fact]
    public void RandiDrawsWholeNumbersInclusiveFromTheScopesStream()
    {
        var expr = Expr.Parse("randi(2, 5)");
        var seen = new HashSet<double>();
        var scope = new DictScope(new Rng(4));
        for (var i = 0; i < 400; i++)
        {
            var v = expr.Evaluate(scope);
            Assert.Equal(Math.Floor(v), v);
            Assert.InRange(v, 2, 5);
            seen.Add(v);
        }
        Assert.Equal(new double[] { 2, 3, 4, 5 }, seen.OrderBy(v => v));
    }

    [Fact]
    public void RandiIsTheSameForTheSameSeed()
    {
        var expr = Expr.Parse("randi(level, level * 3) + randi(0, 10)");
        List<double> Roll()
        {
            var scope = new DictScope(new Rng("randi")).Set("level", 4);
            return Enumerable.Range(0, 20).Select(_ => expr.Evaluate(scope)).ToList();
        }
        Assert.Equal(Roll(), Roll());
        Assert.Contains("level", expr.Identifiers);
    }

    [Theory]
    [InlineData("randi(3, 3)", 3)]
    [InlineData("randi(5, 1) >= 1 && randi(5, 1) <= 5", 1)]
    [InlineData("randi(1.2, 1.8)", 2)]
    public void RandiHandlesEdgeBounds(string source, double expected)
    {
        Assert.Equal(expected, Expr.Parse(source).Evaluate(new DictScope(new Rng(9))));
    }

    [Theory]
    [InlineData("curve(\"linear\", 0.4)", 0.4)]
    [InlineData("curve(\"quadratic\", 0.5)", 0.25)]
    [InlineData("curve(\"quad_out\", 0.5)", 0.75)]
    [InlineData("curve(\"breath\", 0.5)", 1)]
    [InlineData("10 * curve('smooth', 1)", 10)]
    public void CurveReadsNamedCurves(string source, double expected)
    {
        Assert.Equal(expected, Expr.Parse(source).Evaluate(EmptyScope.Instance), 9);
    }

    [Fact]
    public void CurveTakesItsValueFromTheScopeAndValidatesItsName()
    {
        var expr = Expr.Parse("curve(\"quadratic\", held / 2)");
        Assert.Equal(new[] { "held" }, expr.Identifiers);
        Assert.Equal(4, expr.Evaluate(new DictScope().Set("held", 4)), 9);
        Assert.Throws<ExprParseException>(() => Expr.Parse("curve(\"no_such_curve\", 1)"));
        Assert.Throws<ExprParseException>(() => Expr.Parse("curve(linear, 1)"));
        Assert.Throws<ExprParseException>(() => Expr.Parse("randi(1)"));
    }
}
