using HollowWake.Core.Content;
using HollowWake.Core.Effects;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Validation refuses what the simulation would otherwise do silently: an effect naming a status, a charge kind
/// or a pattern no definition carries, a filter word nobody registered, and a layer defining an id twice.
/// </summary>
public class ContentReferenceTests
{
    private static string Scratch(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-" + name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void UnknownNamesInEffectsAreErrors()
    {
        var dir = Scratch("references");
        try
        {
            File.WriteAllText(Path.Combine(dir, "weaves.yaml"), """
kind: weaves
entries:
  - id: t_bad_names
    form: bolt
    threads: [ember]
    effects:
      - { type: apply_status, status: no_such_status }
      - { type: charge, id: no_such_charge, gain: 1, self: true }
      - { type: spawn, pattern: no_such_pattern }
      - { type: area, radius: 2, filter: nobody, effects: [ { type: damage, amount: 1 } ] }
      - { type: consume_status, statuses: [burning, no_such_second] }
""".Replace("\r\n", "\n"));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Where(e => e.Id == "t_bad_names").Select(e => e.Message).ToList();
            Assert.Contains(errors, m => m.Contains("unknown status 'no_such_status'"));
            Assert.Contains(errors, m => m.Contains("unknown charge 'no_such_charge'"));
            Assert.Contains(errors, m => m.Contains("unknown pattern 'no_such_pattern'"));
            Assert.Contains(errors, m => m.Contains("unknown filter 'nobody'"));
            Assert.Contains(errors, m => m.Contains("unknown status 'no_such_second'"));
            Assert.DoesNotContain(errors, m => m.Contains("'burning'"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void VariablesAndKnownNamesPassAndTheCoreContentIsClean()
    {
        ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.Empty(report.Errors);
        Assert.DoesNotContain(report.Warnings, w => w.Message.Contains("Defined twice"));
        // The core bolt applies $thread_status: a variable the compiler resolves, not a name to check.
        Assert.DoesNotContain(report.Errors, e => e.Message.Contains("$thread_status"));
        Assert.True(Filters.Has("enemy") && Filters.Has("all") && !Filters.Has("nobody"));
    }

    [Fact]
    public void ALayerDefinesAnIdOnceButALaterLayerMayReplaceIt()
    {
        var first = Scratch("layer-a");
        var second = Scratch("layer-b");
        try
        {
            const string kind = "kind: charges\nentries:\n  - { id: t_twice, name: Twice, duration: 1, decay: 1 }\n";
            File.WriteAllText(Path.Combine(first, "a.yaml"), kind);
            File.WriteAllText(Path.Combine(first, "b.yaml"), kind);
            File.WriteAllText(Path.Combine(second, "c.yaml"), kind);
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), first, second }, out var report);
            var twice = report.Warnings.Where(w => w.Id == "t_twice" && w.Message.Contains("Defined twice")).ToList();
            var warning = Assert.Single(twice);
            Assert.Contains("b.yaml", warning.Origin ?? "");
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }
}
