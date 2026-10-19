using System.Text.Json;
using HollowWake.Core.Tests;
using Xunit.Abstractions;

var source = args[0];
var results = new List<object>();
var failures = 0;
var tests = new FreeShotGroundFloorTests(new ProbeOutput());
foreach (var data in FaunaProjectileTests.Cases().Select(row => (string)row[0]).Distinct())
foreach (var slope in new[] { -.125f, 0, .125f })
foreach (var preset in new[] { "firebolt", "piercing_arrow" })
foreach (var beyond in new[] { false, true })
{
    string? failure = null;
    try { tests.HumanoidFreeShotsStrikeShortBodiesBeforeAndBeyondCursor(data, slope, preset, beyond); }
    catch (Exception error) { failure = error.ToString(); failures++; }
    results.Add(new { Pattern = data, Slope = slope, Preset = preset, BeyondCursor = beyond, Passed = failure is null, Failure = failure });
}
var report = new { Source = source, Total = results.Count, Passed = results.Count - failures, Failed = failures, Cases = results };
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Fauna free-shot integration: {results.Count - failures} passed, {failures} failed, {results.Count} total.");
return failures == 0 ? 0 : 1;

sealed class ProbeOutput : ITestOutputHelper
{
    public void WriteLine(string message) { }
    public void WriteLine(string format, params object[] args) { }
}
