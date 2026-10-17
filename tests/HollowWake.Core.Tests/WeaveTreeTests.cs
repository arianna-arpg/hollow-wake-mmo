using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// A form's tree (row D23): nodes bought with the points a weave's level grants, in order, through links,
/// past exclusions, with ranks; their modifiers, patches and glyphs folded by the compiler; a seat's tree
/// cleared for a station's reset; and validation of the tree a form declares.
/// </summary>
public class WeaveTreeTests
{
    private static WeaveSpec Tree(int level, params string[] nodes) => new() { Form = "t_tree", Level = level, Tree = nodes.ToList() };

    private static WeaveCompiler Compiler => new(CastingTestContent.Registry, Effects.EffectRegistry.Shared);

    [Fact]
    public void RanksBoughtWithPointsStackTheirModifiers()
    {
        // Level 3 grants two points (rule casting.tree_points is max(0, level - 1)): both ranks of 'wide'.
        var weave = Compiler.Compile(Tree(3, "wide", "wide"));
        Assert.Equal(2, weave.TreePoints);
        Assert.Equal(2, weave.TreeSpent);
        var wide = Assert.Single(weave.Tree);
        Assert.Equal(2, wide.Ranks);
        Assert.Empty(weave.DormantNodes);

        var arena = CastingArena.Create();
        arena.Begin(arena.Slot(Tree(3, "wide", "wide")));
        Assert.Equal(4, Assert.Single(arena.Custom("t_tree")).Data["radius"], 6);
    }

    [Fact]
    public void NodesNeedALinkBoughtFirstAPointAndNoExclusion()
    {
        var compiler = Compiler;

        // Strict compiles refuse a node that cannot count; the dormant fit keeps it spent with its reason.
        Assert.False(compiler.TryCompile(Tree(3, "far"), out _, out var unlinked));
        Assert.Contains("links to none", unlinked);
        var dormant = compiler.Compile(Tree(3, "far", "wide"), GlyphFit.Dormant);
        Assert.Equal("far", Assert.Single(dormant.DormantNodes).Id);
        Assert.Equal("wide", Assert.Single(dormant.Tree).Node.Id);

        Assert.Contains("cannot be held with 'far'", compiler.Compile(Tree(4, "wide", "far", "near"), GlyphFit.Dormant).DormantNodes.Single().Reason);
        Assert.Contains("needs a point", compiler.Compile(Tree(2, "wide", "far"), GlyphFit.Dormant).DormantNodes.Single().Reason);
        Assert.Contains("opens at weave level 2", compiler.Compile(Tree(1, "wide"), GlyphFit.Dormant).DormantNodes.Single().Reason);
        Assert.Contains("is not a node", compiler.Compile(Tree(3, "nowhere"), GlyphFit.Dormant).DormantNodes.Single().Reason);
        Assert.Contains("no rank left", compiler.Compile(Tree(4, "wide", "wide", "wide"), GlyphFit.Dormant).DormantNodes.Single().Reason);
    }

    [Fact]
    public void NodePatchesEditTheGraphAndNodeGlyphsJoinAsGrafts()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot(Tree(3, "wide", "far")));
        Assert.Single(arena.Custom("t_tree_far"));
        Assert.Empty(arena.Custom("t_tree"));

        var weave = Compiler.Compile(Tree(4, "wide", "far", "grafted"));
        Assert.Contains(weave.Glyphs, g => g.Id == "t_repeat");
        Assert.Empty(weave.Grafts);
        Assert.True(weave.Modifiers.Any(m => m.Attribute == "repeat.count"));
    }

    [Fact]
    public void AResetClearsASeatsTreeAndGivesItsPointsBack()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot(Tree(3, "wide", "wide"));
        var index = arena.SlotIndex(slot);
        Assert.Equal((2, 2), arena.Runtime.TreePoints(arena.Sim, slot));

        Assert.True(arena.Runtime.ResetTree(arena.Sim, arena.Caster, index, out var reason), reason);
        Assert.Empty(slot.Spec.Tree);
        Assert.Equal(2, Assert.Single(arena.Of<WeaveTreeReset>()).Refunded);
        Assert.Equal((0, 2), arena.Runtime.TreePoints(arena.Sim, slot));
        arena.Begin(slot);
        Assert.Equal(2, Assert.Single(arena.Custom("t_tree")).Data["radius"], 6);

        Assert.False(arena.Runtime.ResetTree(arena.Sim, arena.Caster, index, out var nothing));
        Assert.Equal("no_tree", nothing);
    }

    [Fact]
    public void TheTorrentsTreeFoldsWithItsGlyphAndItsPatch()
    {
        var compiler = new WeaveCompiler(CoreContent.Registry, Effects.EffectRegistry.Shared);
        var pour = compiler.Compile(new WeaveSpec { Form = "torrent", Threads = { "tide" }, Level = 5, Tree = { "swelling", "quick_pour", "quick_pour", "still_water" } });
        Assert.Equal(4, pour.TreeSpent);
        Assert.Contains(pour.Glyphs, g => g.Id == "bastion");
        Assert.Contains("\"angle\":60", pour.Specs[0].Params.ToJsonString());

        var fan = compiler.Compile(new WeaveSpec { Form = "torrent", Threads = { "tide" }, Level = 3, Tree = { "swelling", "broad_stream" } });
        Assert.Contains("\"angle\":90", fan.Specs[0].Params.ToJsonString());
    }

    [Fact]
    public void ATreeMustNameItsNodesAndReachEveryOneFromARoot()
    {
        const string layer = """
kind: forms
entries:
  - id: t_bad_tree
    targeting: self
    cast_mode: instant
    min_threads: 0
    effects: [ { type: event, name: t_bad_tree } ]
    tree:
      nodes:
        - { id: root, modifiers: [ { attribute: area.radius, form: flat, value: 1 } ] }
        - { id: lost, links: [ ghost ], modifiers: [ { attribute: area.radius, form: flat, value: 1 } ] }
        - { id: loop_a, links: [ loop_b ], modifiers: [ { attribute: area.radius, form: flat, value: 1 } ] }
        - { id: loop_b, links: [ loop_a ], excludes: [ nowhere ], modifiers: [ { attribute: area.radius, form: flat, value: 1 } ] }
""";
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-trees-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "trees.yaml"), layer.Replace("\r\n", "\n"));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Where(e => e.Id == "t_bad_tree").Select(e => e.Message).ToList();
            Assert.Contains(errors, e => e.Contains("tree.lost links to unknown node 'ghost'"));
            Assert.Contains(errors, e => e.Contains("tree.loop_b excludes unknown node 'nowhere'"));
            Assert.Contains(errors, e => e.Contains("tree.loop_a is unlinked"));
            Assert.Contains(errors, e => e.Contains("tree.loop_b is unlinked"));
            Assert.Contains(errors, e => e.Contains("tree.lost is unlinked"));
            Assert.DoesNotContain(errors, e => e.Contains("tree.root"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
