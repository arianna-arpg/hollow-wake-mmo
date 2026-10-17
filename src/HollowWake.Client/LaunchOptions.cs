using Godot;

namespace HollowWake.Client;

/// <summary>
/// Options the launcher (or a shortcut) passes on the command line. <c>--host H</c> and <c>--port P</c>
/// fill the login panel's server fields; <c>--host=H</c> works too. Both the engine arguments and the
/// user arguments after <c>--</c> are read, so <c>HollowWakeMMO --host H</c> from the launcher and
/// <c>godot --path src/HollowWake.Client -- --host H</c> from a checkout behave the same.
/// </summary>
public readonly record struct LaunchOptions(string Host, int Port, bool ServerGiven)
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 7777;

    public static LaunchOptions FromCommandLine() =>
        Parse(OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToList());

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var host = DefaultHost;
        var port = DefaultPort;
        var given = false;
        for (var i = 0; i < args.Count; i++)
        {
            var (name, value) = Split(args, ref i);
            if (name == "--host" && !string.IsNullOrWhiteSpace(value))
            {
                host = value.Trim();
                given = true;
            }
            else if (name == "--port" && int.TryParse(value, out var p) && p is > 0 and <= 65535)
            {
                port = p;
                given = true;
            }
        }
        return new LaunchOptions(host, port, given);
    }

    // "--name=value" or "--name value"; i moves past a consumed value.
    private static (string Name, string? Value) Split(IReadOnlyList<string> args, ref int i)
    {
        var arg = args[i];
        var eq = arg.IndexOf('=');
        if (arg.StartsWith("--") && eq > 2)
        {
            return (arg[..eq], arg[(eq + 1)..]);
        }
        if ((arg == "--host" || arg == "--port") && i + 1 < args.Count && !args[i + 1].StartsWith("--"))
        {
            i++;
            return (arg, args[i]);
        }
        return (arg, null);
    }
}
