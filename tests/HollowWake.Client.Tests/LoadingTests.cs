using System.Text.Json.Nodes;
using HollowWake.Core.Content;
using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public sealed class LoadingTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HollowWake.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
    private static LoadingSpec Spec() => LoadingSpec.FromDocument(File.ReadAllText(Path.Combine(Root(), "content/core/ui/loading.json")));
    [Theory]
    [InlineData("labels", "retry")]
    [InlineData("palette", "gate")]
    [InlineData("rules", "step")]
    [InlineData("wisp", "parts")]
    public void IncompleteLoadingContentIsRejectedBeforeTheClientUsesIt(string section, string key)
    {
        var document=JsonNode.Parse(File.ReadAllText(Path.Combine(Root(),"content/core/ui/loading.json")))!;
        document["entries"]![0]!["meta"]![section]!.AsObject().Remove(key);
        var layer=Path.Combine(Path.GetTempPath(),"hollowwake-loading-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layer);
        try {
            File.WriteAllText(Path.Combine(layer,"loading.json"),document.ToJsonString());
            ContentRegistry.Load(new[]{layer},out var report);
            Assert.True(report.ErrorCount>0);
            Assert.Contains(report.Errors,e=>e.Message.Contains(key,StringComparison.OrdinalIgnoreCase));
        } finally { Directory.Delete(layer,true); }
    }
    [Fact]
    public void OriginalReferenceTracesMatchInEveryDirection()
    {
        var reference = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests/fixtures/loading-reference.json")))!;
        foreach (var course in reference["cases"]!.AsArray())
        {
            var run = new SpiritRun(Spec(), course!["direction"]!.GetValue<string>(), course["seed"]!.GetValue<uint>());
            foreach (var frame in Enumerable.Range(0, 1800))
            {
                var phase = frame % 240;
                run.Step(1.0 / 30, phase < 60 ? 1 : phase < 120 ? -1 : 0, phase >= 120 ? frame / 240 % 2 != 0 ? -150 : 150 : null);
                var sample = course["samples"]!.AsArray().FirstOrDefault(s => s!["frame"]!.GetValue<int>() == frame);
                if (sample is null) continue;
                var values = new Dictionary<string,double> { ["time"]=run.Time, ["lane"]=run.Lane, ["distance"]=run.Distance, ["speed"]=run.Speed,
                    ["passed"]=run.Passed, ["hits"]=run.Hits, ["collected"]=run.Collected, ["boostGates"]=run.BoostGates,
                    ["currentsTaken"]=run.CurrentsTaken, ["dash"]=run.Dash, ["hinder"]=run.Hinder };
                foreach (var (key,value) in values) Assert.Equal(sample[key]!.GetValue<double>(), value, 7);
                var gates = sample["gates"]!.AsArray();
                Assert.Equal(gates.Count, run.Gates.Count);
                for(var i=0;i<gates.Count;i++)
                {
                    Assert.Equal(gates[i]!["id"]!.GetValue<int>(),run.Gates[i].Id);
                    Assert.Equal(gates[i]!["u"]!.GetValue<double>(),run.Gates[i].U,7);
                    var openings=gates[i]!["openings"]!.AsArray();
                    Assert.Equal(openings.Count,run.Gates[i].Openings.Count);
                    for(var j=0;j<openings.Count;j++)
                    {
                        Assert.Equal(openings[j]!["lane"]!.GetValue<double>(),run.Gates[i].Openings[j].Lane,7);
                        Assert.Equal(openings[j]!["width"]!.GetValue<double>(),run.Gates[i].Openings[j].Width,7);
                    }
                }
            }
        }
    }
    [Fact]
    public void StaleWorkCannotCloseOrFailTheCurrentPreparation()
    {
        var session = new LoadingSession();
        var old = session.Begin("Old");
        var current = session.Begin("New");
        old.Update("Wrong"); old.Fail("Wrong","Wrong"); old.Finish();
        Assert.True(current.Current); Assert.Equal("New",session.Label); Assert.False(session.Failed);
        current.Update("Land", completed: 3, total: 10);
        Assert.Equal(.3,session.Fraction);
        current.Update("Unknown", completed: double.NaN, total: 10);
        Assert.Null(session.Fraction);
        current.Fail("Failed", "Retry this stage", () => current.Update("Retrying"));
        Assert.True(session.Failed);
        session.Retry!();
        Assert.False(session.Failed); Assert.Null(session.Retry); Assert.Equal("Retrying", session.Label);
        current.Finish();
        Assert.False(session.Active); Assert.False(current.Current);
    }
    [Fact]
    public void CentralPierCollisionResetsPaceAndBeatsCurrentCollection()
    {
        var run=new SpiritRun(Spec(),"down",160) { Time=1, Lane=0, Streak=20, Dash=2 };
        run.Gates = new() { new(99,run.Spec.Rules.PlayerU,new() { new(-130,114),new(130,114) }) };
        run.Currents = new() { new(1,99,run.PlayerU,0,"opening") };
        Assert.Contains((-73.0,73.0),run.Solids(run.Gates[0]));
        run.Step(1.0/120,0);
        Assert.Equal(1,run.Hits); Assert.Equal(0,run.Streak); Assert.Equal(0,run.Dash); Assert.Equal(0,run.CurrentsTaken);
    }
    [Fact]
    public void PointingAndDrawingShareTheSameTransform()
    {
        foreach(var direction in new[]{"down","left","right"})
        foreach(var size in new[]{(320,160),(400,900),(1600,630)})
        {
            var layout=new SpiritLayout(size.Item1,size.Item2,direction,Spec().Rules);
            var p=layout.Point(230,125);
            Assert.Equal(125,layout.Lane(p.X,p.Y),7);
        }
    }
}
