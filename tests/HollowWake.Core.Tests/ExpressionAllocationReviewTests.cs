using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using Xunit;

namespace HollowWake.Core.Tests;

public class ExpressionAllocationReviewTests
{
    [Fact]
    public void WarmSmallExpressionCallsAllocateNothingOnTheirExecutingThread()
    {
        var scope = new DictScope(new Rng(3)).Set("level", 12).Set("might", 18).Set("vigor", 15)
            .Set("weapon.damage.min", 5).Set("weapon.damage.max", 9).Set("accuracy", 180)
            .Set("evasion", 60).Flag("stationary");
        var formulas = new[]
        {
            Expr.Parse("40 + level * 10 + vigor * 4"),
            Expr.Parse("lerp(weapon.damage.min, weapon.damage.max, rand()) * (1 + might * 0.02)"),
            Expr.Parse("has(\"stationary\") ? 1.35 : 1"),
            Expr.Parse("clamp(accuracy / max(1, accuracy + evasion * 0.25), 0.05, 1)"),
        };
        for (var i = 0; i < 100_000; i++) foreach (var formula in formulas) formula.Evaluate(scope);
        // The CLI's process-wide counter can include unrelated managed runtime activity.
        // This pins the budget's actual small expression calls without changing that retained benchmark.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0.0;
        for (var i = 0; i < 100_000; i++) foreach (var formula in formulas) checksum += formula.Evaluate(scope);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0);
        Assert.Equal(0L, allocated);
    }
}
