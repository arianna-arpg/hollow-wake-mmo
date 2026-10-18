using HollowWake.Core.Content;
using HollowWake.Core.Evaluation;
using Xunit;

namespace HollowWake.Core.Tests;

public class TournamentOutcomeTests
{
    [Fact]
    public void MutualProjectileKillsAreADraw()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-tournament-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "forms.yaml"), """
kind: forms
entries:
  - id: review_mutual_bolt
    name: Mutual bolt
    tags: [spell, projectile]
    targeting: enemy
    min_threads: 0
    cast_time: 0.1
    cooldown: 10
    attributes: { range: 20 }
    effects:
      - type: projectile
        speed: 10
        range: 20
        on_hit: [{ type: damage, amount: 10000, threads: [mind], can_crit: false }]
""");
            File.WriteAllText(Path.Combine(dir, "weaves.yaml"), """
kind: weaves
entries:
  - { id: review_bolt, form: review_mutual_bolt }
""");
            File.WriteAllText(Path.Combine(dir, "patterns.yaml"), """
kind: patterns
entries:
  - id: review_duelist
    name: Review duelist
    extends: monster
    level: 1
    components:
      stats: { attributes: { life.max: 10, move.speed: 0, mana.max: 1000 } }
      weaves: { list: [review_bolt] }
      behavior: { id: aggressive_ranged }
""");
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            Assert.Empty(report.Errors);
            // This seed starts both casts together; both lethal flights arrive in the same tick.
            var result = Tournament.Duel(registry, new Pairing("review_duelist", "review_duelist"), 7, 5);
            Assert.Equal(0, result.SideA.LifeLeft);
            Assert.Equal(0, result.SideB.LifeLeft);
            Assert.Equal(1, result.SideA.Hits);
            Assert.Equal(1, result.SideB.Hits);
            Assert.Equal("draw", result.Outcome);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
