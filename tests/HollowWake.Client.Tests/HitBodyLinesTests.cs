using System.Text.Json;
using HollowWake.Client.World.Logic;
using HollowWake.Protocol;
using Xunit;

namespace HollowWake.Client.Tests;
public class HitBodyLinesTests
{
    [Fact]
    public void BothAdaptersDrawTheSameAuthoritativeVolumes()
    {
        var root=Path.GetFullPath(Path.Combine(ThemeContentTests.FindContentDir(),"../.."));
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/hit-body-lines.json")));
        foreach(var row in fixture.RootElement.EnumerateArray()) {
            var body=row.GetProperty("body").Deserialize<HitBodyInfo>(MessageSerializer.Options)!;
            var lines=HitBodyLines.Build(body,row.GetProperty("radius").GetSingle(),row.GetProperty("height").GetSingle(),16);
            var expected=row.GetProperty("lines");Assert.Equal(expected.GetArrayLength(),lines.Count);
            for(var i=0;i<lines.Count;i++) {
                Assert.InRange(MathF.Abs(lines[i].X-expected[i][0].GetSingle()),0,.00001f);
                Assert.InRange(MathF.Abs(lines[i].Y-expected[i][1].GetSingle()),0,.00001f);
                Assert.InRange(MathF.Abs(lines[i].Z-expected[i][2].GetSingle()),0,.00001f);
            }
        }
    }
    [Fact]
    public void UnknownVolumesDoNotInventACylinder()
    {
        var unknown=new HitBodyInfo("mod",0,new(){new("mod_shape",0,0,.5f,1,.5f)});
        Assert.False(HitBodyLines.Supported(unknown));Assert.Empty(HitBodyLines.Build(unknown,1,2));
    }
}
