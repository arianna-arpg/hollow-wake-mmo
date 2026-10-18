using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Protocol;
using HollowWake.Server;
using HollowWake.Server.Config;
using HollowWake.Server.Net;
using HollowWake.Server.World;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HollowWake.Server.Tests;

public class HitBodyProtocolTests
{
    private static string ContentDir()
    {
        for(var dir=AppContext.BaseDirectory;dir is not null;dir=Path.GetDirectoryName(dir))
            if(Directory.Exists(Path.Combine(dir,"content/core")))return Path.Combine(dir,"content/core");
        throw new DirectoryNotFoundException();
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnatomyIsNegotiatedAndComesFromTheServer(bool optedIn)
    {
        var transport=new LoopbackTransport();var stores=new MemoryStores();
        await using var server=new GameServer(new ServerConfig{ContentLayers=new(){ContentDir()},WorldEvents=false},NullLoggerFactory.Instance,transport,stores,stores,stores);
        await server.StartAsync(CancellationToken.None);
        var client=transport.Connect();
        client.Send(new HelloMsg("hit-body-test",ProtocolInfo.Version){Features=optedIn?new(){ProtocolInfo.HitBodiesFeature}:null});
        server.Step();
        var messages=client.Drain();
        Assert.Equal(optedIn,Assert.Single(messages.OfType<WelcomeMsg>()).Features?.Contains(ProtocolInfo.HitBodiesFeature)==true);
        if(!optedIn) { Assert.Empty(messages.OfType<HitBodiesMsg>());return; }
        var catalog=Assert.Single(messages.OfType<HitBodiesMsg>());
        Assert.Contains(catalog.Bodies,b=>b.Id=="gnasher_whelp");
        Assert.Contains(catalog.Bodies,b=>b.Id=="saddle_gnasher");
        var decoded=Assert.IsType<HitBodiesMsg>(MessageSerializer.Decode(MessageSerializer.Encode(catalog)));
        Assert.Equal(MessageSerializer.ToJson(catalog),MessageSerializer.ToJson(decoded));
    }
    [Fact]
    public void SnapshotDeltaFingerprintAndClearAgreeAboutAnatomy()
    {
        var content=ContentRegistry.Load(new[]{ContentDir()},out _);
        var sim=ZoneBuilder.CreateArena(content,7);
        var e=sim.Service<PatternFactory>().Spawn(sim,"orc_grunt",new(10,10,0));
        var first=Snapshots.Capture(sim,e);var fingerprint=Snapshots.Fingerprint(sim,e);
        Assert.Equal("orc_grunt",first.HitBody);
        var t=sim.World.Get<Transform>(e)!;t.Height*=2;t.Radius*=2;
        var scaled=Snapshots.Capture(sim,e);
        var delta=Snapshots.Delta(first,scaled,out var empty)!;
        Assert.False(empty);Assert.Null(delta.HitBody);
        Assert.Equal(MessageSerializer.ToJson(new EntitySpawnMsg(scaled)),MessageSerializer.ToJson(new EntitySpawnMsg(delta.ApplyTo(first))));
        sim.World.Remove<HitBody>(e);
        var cleared=Snapshots.Capture(sim,e);
        Assert.NotEqual(fingerprint,Snapshots.Fingerprint(sim,e));
        var clear=Snapshots.Delta(scaled,cleared,out empty)!;
        Assert.False(empty);Assert.Equal("",clear.HitBody);Assert.Null(clear.ApplyTo(scaled).HitBody);
    }
}
