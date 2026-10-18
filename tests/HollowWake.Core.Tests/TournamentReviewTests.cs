using System.Globalization;
using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Evaluation;
using HollowWake.Tools;
using Xunit;

namespace HollowWake.Core.Tests;

public class TournamentReviewTests
{
    [Theory]
    [InlineData(-0.004, "0")]
    [InlineData(0.004, "0")]
    [InlineData(-0.006, "-0.01")]
    [InlineData(0.006, "0.01")]
    [InlineData(-1.25, "-1.25")]
    public void TraceNumbersDiscardOnlyTheSignOfRoundedZero(double value, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Tournament.TraceNumber(value));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("scalar")]
    [InlineData("number")]
    [InlineData("duplicate")]
    public void PairingListsMustBePresentNonemptyStringsAndUnique(string problem)
    {
        var directory = Scratch();
        try
        {
            var values = (JsonObject)CoreContent.Registry.Get<RulesDef>("evaluation")!.Values!.DeepClone();
            if (problem == "missing") values.Remove("standard_duels");
            else values["standard_duels"] = problem switch
            {
                "empty" => new JsonArray(),
                "scalar" => JsonValue.Create("wolf:hob"),
                "number" => new JsonArray(12),
                "duplicate" => new JsonArray("wolf:hob", " wolf : hob "),
                _ => throw new ArgumentException(problem),
            };
            File.WriteAllText(Path.Combine(directory, "rules.json"), new JsonObject
            {
                ["kind"] = "rules",
                ["entries"] = new JsonArray(new JsonObject { ["id"] = "evaluation", ["values"] = values }),
            }.ToJsonString());
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), directory }, out var report);
            Assert.Contains(report.Errors, error => error.Id == "evaluation" && error.Message.Contains("standard_duels"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("neutral", "{}", "{}", false)]
    [InlineData("neutral", "{blue: neutral}", "{red: hostile}", false)]
    [InlineData("neutral", "{}", "{red: hostile}", true)]
    [InlineData("hostile", "{}", "{}", true)]
    public void DeclaredDuelFactionsMustBeHostileUsingReciprocalAndDefaultRules(string fallback, string red, string blue, bool valid)
    {
        var directory = Scratch();
        try
        {
            File.WriteAllText(Path.Combine(directory, "factions.yaml"), $$"""
kind: factions
entries:
  - { id: red, default: {{fallback}}, relations: {{red}} }
  - { id: blue, default: {{fallback}}, relations: {{blue}} }
""");
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), directory }, out var report);
            var errors = report.Errors.Where(error => error.Id == "evaluation").ToArray();
            if (valid)
            {
                Assert.Empty(errors);
                Assert.Equal(("red", "blue"), EvaluationRules.FactionsOf(content));
            }
            else
            {
                Assert.Contains(errors, error => error.Message.Contains("mutually hostile"));
                Assert.Throws<InvalidDataException>(() => EvaluationRules.FactionsOf(content));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void CheckingABaselineUsesItsPairingsWhenNoneWereSelected()
    {
        var directory = Scratch();
        try
        {
            var path = Path.Combine(directory, "baseline.json");
            var report = Tournament.Run(CoreContent.Registry, new[] { new Pairing("wolf", "hob") }, 1, 2, false);
            File.WriteAllText(path, TraceBaseline.Of(report).ToJson());
            using var output = new StringWriter();
            Assert.Equal(0, TournamentCommand.Run(CoreContent.Registry, new[] { "--check-hashes", path }, output));
            Assert.Contains("hashes: identical", output.ToString());
            Assert.DoesNotContain("not in the baseline", output.ToString());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Scratch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hollowwake-tournament-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
