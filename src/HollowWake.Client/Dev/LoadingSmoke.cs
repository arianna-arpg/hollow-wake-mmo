using Godot;

namespace HollowWake.Client.Dev;

/// <summary>Real native loading cover, without artificial waits in production entry paths.</summary>
public partial class LoadingSmoke(Main main, string[] args) : Node
{
    public override async void _Ready()
    {
        try
        {
            var shotsAt=Array.IndexOf(args,"--shots");
            var shots=shotsAt>=0?Path.GetFullPath(args[shotsAt+1]):Path.GetFullPath("artifacts/loading-preview");
            Directory.CreateDirectory(shots);
            var screen=main.Loading;
            var stale=screen.Begin("Old work");
            var lease=screen.Begin("Current work");
            stale.Fail("Stale","Must not appear");stale.Finish();
            Check("stale lease",lease.Current&&screen.Session.Label=="Current work");
            lease.Fail("Interrupted","Retry preparation",()=>lease.Update("Retrying"));
            screen.Session.Retry!();
            Check("retry clears failure",!screen.Session.Failed&&screen.Session.Retry is null);
            foreach(var direction in new[]{"down","right","left"})
            {
                screen.Begin(screen.Spec.Text("boot"),direction:direction,cancel:()=>screen.Close());
                // Freeze a reproducible composition. Geometry remains the real model and renderer.
                screen.SetProcess(false);
                screen.Run!.Step(.1,0);
                for(var i=0;i<25;i++)screen.Run.Step(.1,0);
                screen._Process(0);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                var image=GetViewport().GetTexture().GetImage();
                Check(direction+" screenshot",image.SavePng(Path.Combine(shots,direction+".png"))==Error.Ok);
                screen.SetProcess(true);
            }
            var cancelled=false;
            screen.Begin("Cancel check",cancel:()=>{cancelled=true;screen.Close();});
            for(var i=0;i<4;i++)
            {
                Input.ParseInputEvent(new InputEventKey { Keycode=Key.Tab,PhysicalKeycode=Key.Tab,Pressed=true });
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var focus=GetViewport().GuiGetFocusOwner();
                Check("tab stays inside cover",focus is Button&&screen.IsAncestorOf(focus));
                Input.ParseInputEvent(new InputEventKey { Keycode=Key.Tab,PhysicalKeycode=Key.Tab,Pressed=false });
            }
            screen.Session.Cancel!();
            Check("cancel closes cover",cancelled&&!screen.Active);
            GD.Print("LOADING SMOKE PASS");GetTree().Quit();
        }
        catch(Exception ex){GD.PrintErr("LOADING SMOKE FAIL "+ex);GetTree().Quit(1);}
    }
    private static void Check(string label,bool pass)
    {
        GD.Print($"LOADING {(pass?"PASS":"FAIL")} {label}");
        if(!pass)throw new InvalidOperationException(label);
    }
}
