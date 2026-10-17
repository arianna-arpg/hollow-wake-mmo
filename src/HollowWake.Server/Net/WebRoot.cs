namespace HollowWake.Server.Net;

/// <summary>
/// Maps request paths onto a built single-page web client: files by path, <c>index.html</c> for
/// directories and for client-side routes (paths without an extension). Nothing outside the root and
/// no dot-file or dot-directory is ever served. Pure path logic, so it is testable without sockets.
/// </summary>
public sealed class WebRoot
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string Directory { get; }

    public bool Exists => System.IO.Directory.Exists(Directory);

    public WebRoot(string directory)
    {
        Directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }

    /// <summary>The file a URL path maps to, or null when there is nothing to serve.</summary>
    public string? Resolve(string urlPath)
    {
        string relative;
        try
        {
            relative = Uri.UnescapeDataString(urlPath).Replace('\\', '/').TrimStart('/');
        }
        catch (UriFormatException)
        {
            return null;
        }
        if (relative.Contains('\0') || relative.Split('/').Any(s => s.StartsWith('.')))
        {
            return null;
        }
        var full = Path.GetFullPath(Path.Combine(Directory, relative));
        if (!IsInside(full))
        {
            return null;
        }
        if (System.IO.Directory.Exists(full))
        {
            full = Path.Combine(full, "index.html");
        }
        if (File.Exists(full))
        {
            return full;
        }
        // A route of the app gets the app; a missing asset stays missing rather than turning into HTML.
        if (Path.GetExtension(relative).Length == 0)
        {
            var index = Path.Combine(Directory, "index.html");
            return File.Exists(index) ? index : null;
        }
        return null;
    }

    private bool IsInside(string full) =>
        string.Equals(full, Directory, PathComparison) || full.StartsWith(Directory + Path.DirectorySeparatorChar, PathComparison);

    /// <summary>Hashed build output can be cached forever; everything else is revalidated so a rebuild shows up.</summary>
    public string CacheControl(string file)
    {
        var assets = Path.Combine(Directory, "assets") + Path.DirectorySeparatorChar;
        return file.StartsWith(assets, PathComparison) ? "public, max-age=31536000, immutable" : "no-cache";
    }

    public static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        ".txt" => "text/plain; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".wasm" => "application/wasm",
        ".glb" => "model/gltf-binary",
        ".gltf" => "model/gltf+json",
        ".ogg" => "audio/ogg",
        ".wav" => "audio/wav",
        _ => "application/octet-stream",
    };
}
