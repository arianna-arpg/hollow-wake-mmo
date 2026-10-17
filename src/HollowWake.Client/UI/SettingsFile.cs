using Godot;
using FileAccess = Godot.FileAccess;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI;

/// <summary>
/// The settings file of a profile, <c>user://settings/{profile}.json</c>: read at start (defaults for anything
/// missing or malformed), written on every change.
/// </summary>
public static class SettingsFile
{
    private const string Folder = "user://settings";

    public static string PathOf(string profile)
    {
        var safe = new string(profile.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return $"{Folder}/{(safe.Length == 0 ? "default" : safe)}.json";
    }

    /// <summary>Reads a profile's settings; with <paramref name="fresh"/> the profile starts from the defaults.</summary>
    public static Settings Load(OptionsCatalog catalog, string profile, bool fresh = false)
    {
        var path = PathOf(profile);
        if (fresh && FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
        string? text = null;
        if (FileAccess.FileExists(path))
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            text = file?.GetAsText();
        }
        return Settings.Load(catalog, text);
    }

    public static void Save(Settings settings, string profile)
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
        using var file = FileAccess.Open(PathOf(profile), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"settings could not be written to {PathOf(profile)}: {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(settings.Serialize());
    }
}
