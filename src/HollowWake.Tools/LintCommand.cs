using System.Globalization;
using HollowWake.Core.Content;

namespace HollowWake.Tools;

/// <summary>
/// <c>hollowwake lint</c>: the composition lint (<see cref="CompositionLint"/>) over the content layers and the core's
/// C#. Prints the findings grouped by check, smells before notes; <c>--check prefix</c> prints only the checks whose id
/// starts with it. <c>--json file</c> writes the whole report; <c>--code dir</c> names the source root to scan (by
/// default src/HollowWake.Core, found above the working directory or the tool's own the way the tests find
/// content/core) and <c>--no-code</c> skips the scan; <c>--copies n</c> sets the copy threshold. With <c>--json</c>,
/// <c>--baseline file</c> also writes every finding's key there; without it, the run fails naming each finding whose
/// key the baseline lacks, so the baseline may shrink but not grow. Content that does not load fails the run; the
/// findings themselves never do.
/// </summary>
public static class LintCommand
{
    public static int Run(IReadOnlyList<string> contentDirs, Func<string, string, string> opt, Func<string, bool> flag)
    {
        var content = ContentRegistry.Load(contentDirs, out var report);
        if (report.HasErrors)
        {
            foreach (var issue in report.Errors)
            {
                Console.WriteLine(issue);
            }
            Console.WriteLine($"content has {report.ErrorCount} error(s); the lint reads only content that loads");
            return 1;
        }
        string? sourceRoot = null;
        if (!flag("no-code"))
        {
            var given = opt("code", "");
            sourceRoot = given.Length > 0 ? Path.GetFullPath(given) : FindSourceRoot();
            if (sourceRoot is null)
            {
                Console.WriteLine("src/HollowWake.Core was not found above this directory, so the code scan is skipped; pass --code <src root>.");
            }
            else if (!Directory.Exists(sourceRoot))
            {
                Console.WriteLine($"source root {sourceRoot} does not exist");
                return 1;
            }
        }
        var repository = FindRepositoryRoot(sourceRoot ?? Path.GetFullPath(contentDirs[0]));
        var copies = int.Parse(opt("copies", CompositionLint.DefaultCopiedThreshold.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        var findings = new CompositionLint(content, sourceRoot) { CopiedThreshold = copies, RepositoryRoot = repository }.Run();
        var shownRoot = sourceRoot is null ? null : repository is null ? sourceRoot : Path.GetRelativePath(repository, sourceRoot).Replace('\\', '/');
        Console.WriteLine(shownRoot is null ? "code scan: skipped" : $"code scan: {shownRoot}");
        Console.WriteLine();

        var only = opt("check", "");
        foreach (var group in CompositionLint.ByCheck(findings).Where(g => g.Check.StartsWith(only, StringComparison.Ordinal)))
        {
            var skipped = sourceRoot is null && group.Check is CompositionLint.CodeStringSwitch or CompositionLint.CodeFormRead;
            Console.WriteLine(skipped ? $"{group.Check}: skipped, no source root" : $"{group.Check}: {group.Smells} smell(s), {group.Notes} note(s)");
            foreach (var f in group.Findings)
            {
                Console.WriteLine($"  {f.Severity,-5}  {f.Subject}: {f.Message}");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"{findings.Count(f => f.Severity == CompositionLint.Smell)} smell(s), {findings.Count(f => f.Severity == CompositionLint.Note)} note(s)");

        var json = opt("json", "");
        var baseline = opt("baseline", "");
        if (json.Length > 0)
        {
            WriteText(json, CompositionLint.Json(findings, shownRoot, copies));
            Console.WriteLine($"wrote {json}");
            if (baseline.Length > 0)
            {
                WriteText(baseline, CompositionLint.Baseline(findings));
                Console.WriteLine($"wrote baseline {baseline}");
            }
            return 0;
        }
        if (baseline.Length > 0)
        {
            if (!File.Exists(baseline))
            {
                Console.WriteLine($"baseline {baseline} does not exist; write one with --json <file> --baseline {baseline}");
                return 1;
            }
            var beyond = CompositionLint.Beyond(findings, File.ReadAllLines(baseline));
            if (beyond.Count > 0)
            {
                Console.WriteLine($"{beyond.Count} finding(s) beyond baseline {baseline}:");
                foreach (var f in beyond)
                {
                    Console.WriteLine($"  {f.Key}");
                    Console.WriteLine($"      {f.Subject}: {f.Message}");
                }
                return 1;
            }
            Console.WriteLine($"nothing beyond baseline {baseline}");
        }
        return 0;
    }

    private static void WriteText(string path, string text)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    /// <summary>The directory holding HollowWake.sln at or above <paramref name="start"/>, or null.</summary>
    private static string? FindRepositoryRoot(string start)
    {
        var dir = start;
        for (var i = 0; i < 12 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "HollowWake.sln")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
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
                    return Path.GetFullPath(candidate);
                }
                dir = Path.GetDirectoryName(dir);
            }
        }
        return null;
    }
}
