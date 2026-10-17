using Godot;

namespace HollowWake.Client.Dev;

/// <summary>Opt-in login convenience. All account and character decisions still go through the server.</summary>
public partial class QuickPlay(Main main, string[] args) : Node
{
    private readonly ConfigFile _saved = new();
    private string _key = "";
    private int _lists;
    private bool _ready;
    private const string File = "user://quick-play.cfg";

    public override void _Ready()
    {
        var launch = LaunchOptions.FromCommandLine();
        var index = Array.IndexOf(args, "--account");
        var account = index >= 0 && index + 1 < args.Length ? args[index + 1] : "marcher";
        _key = $"{launch.Host}:{launch.Port}/{account}";
        _saved.Load(File);
        main.Login.EnterRequested += id =>
        {
            _saved.SetValue("characters", _key, id);
            _saved.Save(File);
        };
        _lists = main.CharacterLists;
        main.Login.ConnectTo(launch.Host, launch.Port, account);
    }

    public override void _Process(double delta)
    {
        if (main.InWorld && !_ready)
        {
            _ready = true;
            GD.Print("QUICKPLAY READY: entered the world");
        }
        if (_ready || main.CharacterLists == _lists) return;
        _lists = main.CharacterLists;
        var list = main.Login.Characters;
        var remembered = _saved.GetValue("characters", _key, "").AsString();
        var id = UI.Logic.ResumeCharacter.Select(list.Select(c => c.Id).ToArray(), remembered);
        if (id is not null) main.Login.Enter(id);
        // Empty or ambiguous accounts keep the ordinary creation/selection panel visible.
    }
}
