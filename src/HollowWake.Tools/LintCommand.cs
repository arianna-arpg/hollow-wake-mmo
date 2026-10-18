using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HollowWake.Core.Content;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake lint</c>: the composition lint (<see cref="CompositionLint"/>) over the loaded content and the core's
/// C#. Prints the findings grouped by check, smells before notes. <c>--json file</c> also writes them as JSON;
/// <c>--code dir</c> names the source root to scan (by default src/HollowWake.Core, found above the working directory
/// or the tool's own the way the tests find content/core) and <c>--no-code</c> skips the scan; <c>--copies n</c> sets
/// how many definitions must write one formula before it counts as copied; <c>--check prefix</c> keeps the checks
/// whose id starts with it. The lint reports; it never fails.
/// </summary>
public static class LintCommand
{
    public static int Run(ContentRegistry content, Func<string, string, string> opt, Func<string, bool> flag)
    {
        string? sourceRoot = null;
        if (!flag("no-code"))
        {
            var given = opt("code", "");
            sourceRoot = given.Length > 0 ? given : FindSourceRoot();
            if (sourceRoot is null)
            {
                Console.WriteLine("src/HollowWake.Core was not found above this directory, so the code scan is skipped; pass --code <src root>.");
            }
            else if (!Directory.Exists(sourceRoot))
            {
                Console.WriteLine($"source root {sourceRoot} does not exist");
                return 1;
            }
            else
            {
                Console.WriteLine($"code scan: {Path.GetFullPath(sourceRoot)}");
            }
        }
        var copies = int.Parse(opt("copies", "5"), CultureInfo.InvariantCulture);
        var findings = new CompositionLint(content, sourceRoot) { CopiedThreshold = copies }.Run();
        var only = opt("check", "");
        var groups = CompositionLint.ByCheck(findings).Where(g => g.Check.StartsWith(only, StringComparison.Ordinal)).ToList();
        Console.WriteLine();
        foreach (var group in groups)
        {
            var skipped = sourceRoot is null && group.Check is CompositionLint.CodeStringSwitch or CompositionLint.CodeFormRead;
            Console.WriteLine(skipped ? $"{group.Check}: skipped, no source root" : $"{group.Check}: {group.Smells} smell(s), {group.Notes} note(s)");
            foreach (var f in group.Findings)
            {
                Console.WriteLine($"  {f.Severity,-5}  {f.Subject}: {f.Message}");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"{groups.Sum(g => g.Smells)} smell(s), {groups.Sum(g => g.Notes)} note(s)");
        var json = opt("json", "");
        if (json.Length > 0)
        {
            Write(json, groups, sourceRoot, copies);
            Console.WriteLine($"wrote {json}");
        }
        return 0;
    }

    private static void Write(string path, IReadOnlyList<CompositionGroup> groups, string? sourceRoot, int copies)
    {
        var report = new
        {
            source_root = sourceRoot is null ? null : Relative(Path.GetFullPath(sourceRoot)),
            copies,
            smells = groups.Sum(g => g.Smells),
            notes = groups.Sum(g => g.Notes),
            checks = groups.Select(g => new
            {
                check = g.Check,
                smells = g.Smells,
                notes = g.Notes,
                findings = g.Findings.Select(f => new { severity = f.Severity, kind = f.Kind, id = f.Id, file = Relative(f.File), line = f.Line, message = f.Message }),
            }),
        };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(report, options).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>A content origin under the working directory as a relative path (each part of a patched origin); code paths are already relative to the source root.</summary>
    private static string? Relative(string? file)
    {
        if (file is null)
        {
            return null;
        }
        var here = Directory.GetCurrentDirectory();
        return string.Join(" + ", file.Split(" + ").Select(part =>
        {
            if (!Path.IsPathRooted(part))
            {
                return part;
            }
            var relative = Path.GetRelativePath(here, part);
            return (relative.StartsWith("..", StringComparison.Ordinal) ? part : relative).Replace('\\', '/');
        }));
    }

    /// <summary>src/HollowWake.Core above the working directory or the tool's own directory, or null.</summary>
    private static string? FindSourceRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = start;
            for (var i = 0; i < 8 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "HollowWake.Core");
                if (File.Exists(Path.Combine(candidate, "HollowWake.Core.csproj")))
                {
                    return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }
        return null;
    }
}
