using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Content;
using System.Text.Json.Nodes;
using Xunit;

namespace HollowWake.Core.Tests;

public class HitBodyTests
{
    private static readonly ShapeSize Point = new(0, 0, 0, 0);
    private static bool At(DeliveryArena a, Entity e, Vector3 p, float radius = 0) =>
        HitBodies.Touches(a.Sim, e, Shapes.Circle, new(radius, 0, 0, 0), p, Vector2.UnitX, p.Z, p.Z);
    private static Entity Body(DeliveryArena a, params HitVolumeSpec[] volumes)
    {
        var e = a.Body(15, 10);
        var t = a.Sim.World.Get<Transform>(e)!;
        t.Radius = 1; t.Height = 2; t.Yaw = 0;
        a.Sim.World.Add(e, new HitBody { Definition = new HitBodyDef { Id = "test", Volumes = volumes.Select(v => v with
            { Forward = v.Forward / 2, Left = v.Left / 2, Radius = v.Radius / 2 }).ToList() } });
        return e;
    }

    [Fact]
    public void EmptySpaceAboveBesideAndBetweenBodyPartsMisses()
    {
        var a = DeliveryArena.Create();
        var e = Body(a, new() { Left = -.4f, Up = .5f, Radius = .1f, HalfHeight = .5f },
            new() { Left = .4f, Up = .5f, Radius = .1f, HalfHeight = .5f });
        Assert.False(At(a, e, new(15, 10, 1)));
        Assert.True(At(a, e, new(15, 10.4f, 1)));
        Assert.False(At(a, e, new(15, 10.4f, 2.01f)));
        Assert.False(At(a, e, new(15, 10.4f, -.01f)));
        Assert.False(At(a, e, new(15.11f, 10.4f, 1)));
        Assert.True(At(a, e, new(15.15f, 10.4f, 1), .051f));
    }

    [Fact]
    public void LongBodiesRotateAndLiveScalingPreservesOffsets()
    {
        var a = DeliveryArena.Create();
        var e = Body(a, new HitVolumeSpec { Forward = .8f, Up = .5f, Radius = .15f, HalfHeight = .2f });
        Assert.True(At(a, e, new(15.8f, 10, 1)));
        Assert.False(At(a, e, new(15, 10.8f, 1)));
        var t = a.Sim.World.Get<Transform>(e)!;
        t.Yaw = MathF.PI / 2;
        Assert.True(At(a, e, new(15, 10.8f, 1)));
        Assert.False(At(a, e, new(15.8f, 10, 1)));
        t.Radius *= 3;
        Assert.True(At(a, e, new(15, 10.8f, 1))); // Clearance tuning cannot widen anatomy.
        t.Height *= 2;
        Assert.True(At(a, e, new(15, 11.6f, 2)));
        Assert.Equal(new Vector3(15, 11.6f, 2), HitBodies.AimPoint(a.Sim, e));
    }

    [Fact]
    public void SweepsCatchTinyBodiesButNotSideOrOverheadMisses()
    {
        var a = DeliveryArena.Create();
        var e = Body(a, new HitVolumeSpec { Up = .1f, Radius = .04f, HalfHeight = .1f });
        var size = new ShapeSize(.02f, 0, 0, 0);
        float? Sweep(float y, float z) => HitBodies.Sweep(a.Sim,e,Shapes.Circle,size,new(10,y,z),new(20,y,z),Vector2.UnitX,.01f);
        Assert.InRange(Sweep(10,.2f)!.Value,.49f,.50f);
        Assert.Null(Sweep(10.061f,.2f));
        Assert.NotNull(Sweep(10.059f,.2f));
        Assert.Null(Sweep(10,.42f));
        Assert.Null(Sweep(10,-.02f));
        var diagonal=HitBodies.Sweep(a.Sim,e,Shapes.Circle,size,new(14,10,1),new(16,10,-.6f),Vector2.UnitX,.01f);
        Assert.NotNull(diagonal);
    }

    [Theory]
    [InlineData("square", 1.08f, 1.08f)]
    [InlineData("line", 1.08f, .58f)]
    [InlineData("bar", .58f, 1.08f)]
    public void ShapeCornersDoNotTurnRoundBodyPaddingIntoASquare(string name,float x,float y)
    {
        var shape=Shapes.Get(name); var size=new ShapeSize(1,90,1,0);
        Assert.False(shape.Contains(size,new(x,y),Vector2.UnitX,.1f));
        Assert.True(shape.Contains(size,new(x,y),Vector2.UnitX,.12f));
    }

    [Fact]
    public void AreaObservationAndExecutionShareOptInBodyAndVerticalContact()
    {
        var a=DeliveryArena.Create(); var e=Body(a,new HitVolumeSpec {Up=.5f,Radius=.3f,HalfHeight=.5f});
        var effect=a.Effect("""{"type":"area","center":"point","contact":"body","radius":1,"height":0.1,"effects":[{"type":"event","name":"t_hit"}]}""");
        var context=a.Context(new Vector3(13.75f,10,1));
        var plan=DeliveryForecasts.Describe(effect,context).PrimaryRegion!.Value;
        Assert.False(plan.ContainsCenter(a.Sim.World.Get<Transform>(e)!.Position));
        Assert.True(plan.IsCandidate(context,e,a.Sim.World.Get<Transform>(e)!.Position));
        effect.Execute(context); Assert.Contains(e,a.Struck());
        a.Events.Clear();
        effect.Execute(a.Context(new(13.75f,10,2.1f))); Assert.Empty(a.Struck());
    }

    [Fact]
    public void HugeScaledBodiesAreNotDroppedByTheOldFixedBroadPhasePadding()
    {
        var a=DeliveryArena.Create(); var e=Body(a,new HitVolumeSpec {Shape="cylinder",Forward=0,Up=.5f,Radius=1,HalfHeight=.5f});
        var t=a.Sim.World.Get<Transform>(e)!; t.Radius=8; t.Height=12;
        a.Run("""{"type":"projectile","speed":1,"range":20,"radius":0.1,"on_hit":[{"type":"event","name":"t_hit"}]}""",new(30,10,0),Vector2.UnitX);
        a.Tick(1); Assert.Contains(e,a.Struck());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CandidateObservationTranslatesAnatomyWithoutMovingTheLiveBody(bool profiled)
    {
        var a = DeliveryArena.Create();
        var e = Body(a, new HitVolumeSpec { Shape = "cylinder", Up = .5f, Radius = .3f, HalfHeight = .5f });
        if (!profiled) a.Sim.World.Remove<HitBody>(e);
        var position = a.Sim.World.Get<Transform>(e)!.Position;
        var effect = a.Effect("""{"type":"area","center":"point","contact":"body","radius":1,"height":0.1,"effects":[{"type":"event","name":"t_hit"}]}""");
        var context = a.Context(new(13.75f, 10, 1));
        var plan = DeliveryForecasts.Describe(effect, context).PrimaryRegion!.Value;
        Assert.True(plan.IsCandidate(context, e, position));
        Assert.False(plan.IsCandidate(context, e, position + new Vector3(4, 0, 0)));
        Assert.False(plan.IsCandidate(context, e, position + new Vector3(0, 0, 3)));
        Assert.Equal(position, a.Sim.World.Get<Transform>(e)!.Position);
        effect.Execute(context);
        Assert.Contains(e, a.Struck());
    }

    [Theory]
    [InlineData("gnasher_whelp",.36f)]
    [InlineData("goblin_skirmisher",.9f)]
    [InlineData("orc_warlord",2.48f)]
    [InlineData("skeleton_warrior",1.8f)]
    public void AuthoredAnatomyKeepsStatureClearanceAndMassIndependent(string pattern,float height)
    {
        var a=DeliveryArena.Create(); var e=a.Body(20,20,pattern:pattern);
        var t=a.Sim.World.Get<Transform>(e)!; var body=a.Sim.World.Get<HitBody>(e);
        Assert.NotNull(body); Assert.Equal(height,t.Height,4);
        var radius=t.Radius; var mass=Physicality.Mass(a.Sim,e);
        a.Sim.World.Remove<HitBody>(e);
        Assert.Equal(radius,t.Radius); Assert.Equal(mass,Physicality.Mass(a.Sim,e));
    }

    [Fact]
    public void ProfilesRejectBadShapesSizesAndMissingInheritedReferences()
    {
        var c=ContentRegistry.Load(new[]{CoreContent.FindContentDir()},out _);
        ContentKinds.TryGet("hit_bodies",out var kind);
        foreach(var v in new[]{new HitVolumeSpec{Shape="mesh",Radius=.5f,Up=.5f,HalfHeight=.5f},
            new HitVolumeSpec{Radius=-1,Up=.5f,HalfHeight=.5f},new HitVolumeSpec{Radius=1,Up=.1f,HalfHeight=.5f}}) {
            var d=new HitBodyDef{Id="invalid",Volumes=new(){v}};var report=new ValidationReport();
            kind!.Validate!(d,new ValidationContext(c,report,"hit_bodies",d.Id,null));Assert.True(report.HasErrors);
        }
        var child=new PatternDef{Id="bad_child",Extends="skeleton_warrior",Components={ ["hit_body"]=new JsonObject{["profile"]="absent"}}};
        c.With(child);var invalid=new ValidationReport();ContentKinds.TryGet("patterns",out var patternKind);
        patternKind!.Validate!(child,new ValidationContext(c,invalid,"patterns",child.Id,null));Assert.True(invalid.HasErrors);
        ContentKinds.TryGet("rules", out var ruleKind);
        var badRule = new RulesDef { Id = "body", Values = new JsonObject { ["player_hit_profile"] = "typo" } };
        var badReference = new ValidationReport();
        ruleKind!.Validate!(badRule, new ValidationContext(c, badReference, "rules", "body", null));
        Assert.True(badReference.HasErrors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TargetedBoltsAimDownAtWhelpsWhileHorizontalBoltsPassAbove(bool targeted)
    {
        var a=DeliveryArena.Create();var e=a.Body(15,10,pattern:"gnasher_whelp");
        a.Run("""{"type":"projectile","aim":"target","radius":0.05,"speed":30,"range":10,"on_hit":[{"type":"event","name":"t_hit"}]}""",
            new(20,10,0),Vector2.UnitX,target:targeted?e:Entity.None);
        a.Tick(8);Assert.Equal(targeted,a.Struck().Contains(e));
    }

    [Fact]
    public void SweptContactsStrikeTheNearBodyFirstRegardlessOfGridInsertionOrder()
    {
        var a=DeliveryArena.Create();var far=a.Body(15,10);var near=a.Body(13,10);
        a.Run("""{"type":"projectile","radius":0.05,"speed":200,"range":20,"on_hit":[{"type":"event","name":"t_hit"}]}""",new(30,10,0),Vector2.UnitX);
        a.Tick(1);Assert.Equal(new[]{near},a.Struck());Assert.DoesNotContain(far,a.Struck());
    }

    [Theory]
    [InlineData("\"chains\":1")]
    [InlineData("\"return\":\"caster\"")]
    public void ChainAndReturnDiscardTheRemainderOfTheOldSweptPath(string transition)
    {
        var a=DeliveryArena.Create();var near=a.Body(13,10);var far=a.Body(18,10);
        a.Run("{\"type\":\"projectile\",\"radius\":0.05,\"speed\":200,\"range\":20,"+transition+
            ",\"on_hit\":[{\"type\":\"event\",\"name\":\"t_hit\"}]}",new(30,10,0),Vector2.UnitX);
        a.Tick(1);Assert.Equal(new[]{near},a.Struck());Assert.DoesNotContain(far,a.Struck());
        var flight=Assert.Single(a.Sim.World.Query<Transform,Projectile>());
        Assert.InRange(flight.Item2.Position.X,12,13);
    }

    [Fact]
    public void ThinSectorRimHasFiniteRoundContactAtItsEndpoint()
    {
        var shape=Shapes.Get("sector");var size=new ShapeSize(1,90,0,1);
        Assert.True(shape.Contains(size,new(.7071068f,.72f),Vector2.UnitX,.02f));
        Assert.False(shape.Contains(size,new(.7071068f,.74f),Vector2.UnitX,.02f));
    }

    [Fact]
    public void SectorApexUsesOnlyTheActualRoundBodyPadding()
    {
        var shape = Shapes.Get("sector"); var size = new ShapeSize(2, 90, 0, 0);
        Assert.True(shape.Contains(size, new(-.05f, 0), Vector2.UnitX, .051f));
        Assert.False(shape.Contains(size, new(-.05f, 0), Vector2.UnitX, .049f));
        Assert.False(shape.Contains(size, new(-.005f, 0), Vector2.UnitX, 0));
    }

    [Fact]
    public void CachedBoundsIncludeSameTickSpawnsAndExplicitResizeInvalidation()
    {
        var a = DeliveryArena.Create(); var old = HitBodies.Bounds(a.Sim);
        var e = a.Body(20, 20, pattern: "orc_warlord");
        Assert.True(HitBodies.Bounds(a.Sim).Height > old.Height);
        a.Sim.World.Get<Transform>(e)!.Height *= 4;
        HitBodies.InvalidateBounds(a.Sim);
        Assert.True(HitBodies.Bounds(a.Sim).Height > old.Height * 4);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) _ = HitBodies.Bounds(a.Sim);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void RarityAndOwnedBodiesKeepTheSameProfileAndScaleTheLiveAnatomy()
    {
        var a=DeliveryArena.Create();var factory=a.Sim.Service<PatternFactory>();
        var normal=factory.Spawn(a.Sim,"orc_grunt",new(20,20,0),new SpawnOptions{Rarity="normal"});
        var rare=factory.Spawn(a.Sim,"orc_grunt",new(25,20,0),new SpawnOptions{Rarity="rare"});
        var n=a.Sim.World.Get<Transform>(normal)!;var r=a.Sim.World.Get<Transform>(rare)!;
        var ratio=r.Height/n.Height;
        Assert.True(ratio>1);Assert.Same(a.Sim.World.Get<HitBody>(normal)!.Definition,a.Sim.World.Get<HitBody>(rare)!.Definition);
        Assert.Equal((HitBodies.AimPoint(a.Sim,normal).Z-n.Position.Z)*ratio,HitBodies.AimPoint(a.Sim,rare).Z-r.Position.Z,5);
        a.Run("""{"type":"summon","pattern":"orc_grunt","count":1,"max_active":1,"duration":0}""",new(12,10,0));
        var (summoned,_) = Assert.Single(a.Sim.World.Query<Minion>());
        var t=a.Sim.World.Get<Transform>(summoned)!;
        Assert.Same(a.Sim.World.Get<HitBody>(normal)!.Definition,a.Sim.World.Get<HitBody>(summoned)!.Definition);
        var old=HitBodies.AimPoint(a.Sim,summoned)-t.Position;
        t.Radius*=2;t.Height*=2;
        Assert.True(Vector3.Distance(old*2,HitBodies.AimPoint(a.Sim,summoned)-t.Position)<1e-5f);
    }
}
