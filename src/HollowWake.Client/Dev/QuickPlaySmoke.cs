using Godot;

namespace HollowWake.Client.Dev;

/// <summary>Observes real quick play through a delayed server connection, then checks gameplay input.</summary>
public partial class QuickPlaySmoke(Main main, string[] args) : Node
{
    public override async void _Ready()
    {
        try
        {
            var at = Array.IndexOf(args, "--shots");
            var shots = Path.GetFullPath(at >= 0 ? args[at + 1] : "artifacts/quick-play-smoke");
            Directory.CreateDirectory(shots);
            await Until(() => main.CharacterLists > 0 && main.Loading.Active && !main.InWorld);
            Check("automatic selection owns the Crossing", main.Loading.Session.Cancel is not null);
            await Shot(Path.Combine(shots, "entry.png"));
            await Until(() => main.InWorld && main.World.Self is not null && !main.Loading.Active);
            Check("hero and terrain are ready before the cover closes", main.World.BuiltChunks > 0);
            // Allow input quarantine to settle, without changing production's readiness path.
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            var before = main.World.Self!.State;
            Input.ActionPress("move_forward");
            await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            Input.ActionRelease("move_forward");
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var after = main.World.Self!.State;
            Check("gameplay input moves the hero after loading", Math.Pow(after.X - before.X, 2) + Math.Pow(after.Y - before.Y, 2) > .25);
            await Shot(Path.Combine(shots, "playing.png"));
            GD.Print("QUICKPLAY SMOKE PASS");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            Input.ActionRelease("move_forward");
            GD.PrintErr("QUICKPLAY SMOKE FAIL " + ex);
            GetTree().Quit(1);
        }
    }

    private async Task Until(Func<bool> ready)
    {
        var deadline = Time.GetTicksMsec() + 60000;
        while (!ready() && Time.GetTicksMsec() < deadline)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check("startup stage completed before timeout", ready());
    }

    private async Task Shot(string file)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check("captured " + Path.GetFileName(file), image.SavePng(file) == Error.Ok);
    }

    private static void Check(string name, bool pass)
    {
        GD.Print($"QUICKPLAY {(pass ? "PASS" : "FAIL")} {name}");
        if (!pass) throw new InvalidOperationException(name);
    }
}
