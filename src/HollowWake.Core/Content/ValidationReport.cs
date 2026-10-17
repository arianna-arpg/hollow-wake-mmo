using System.Text;

namespace HollowWake.Core.Content;

public enum Severity
{
    Info,
    Warning,
    Error,
}

public sealed record ValidationIssue(Severity Severity, string Kind, string Id, string Message, string? Origin)
{
    public override string ToString()
    {
        var where = Origin is null ? "" : $" ({Origin})";
        var subject = Id.Length == 0 ? Kind : $"{Kind}/{Id}";
        return $"{Severity.ToString().ToUpperInvariant()} {subject}: {Message}{where}";
    }
}

/// <summary>Everything the content loader and validators have to say. Errors block loading in strict mode.</summary>
public sealed class ValidationReport
{
    private readonly List<ValidationIssue> _issues = new();

    public IReadOnlyList<ValidationIssue> Issues => _issues;

    public int ErrorCount => _issues.Count(i => i.Severity == Severity.Error);
    public int WarningCount => _issues.Count(i => i.Severity == Severity.Warning);
    public bool HasErrors => ErrorCount > 0;

    public void Error(string kind, string id, string message, string? origin = null) => _issues.Add(new ValidationIssue(Severity.Error, kind, id, message, origin));
    public void Warn(string kind, string id, string message, string? origin = null) => _issues.Add(new ValidationIssue(Severity.Warning, kind, id, message, origin));
    public void Info(string kind, string id, string message, string? origin = null) => _issues.Add(new ValidationIssue(Severity.Info, kind, id, message, origin));

    public IEnumerable<ValidationIssue> Errors => _issues.Where(i => i.Severity == Severity.Error);
    public IEnumerable<ValidationIssue> Warnings => _issues.Where(i => i.Severity == Severity.Warning);

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var i in _issues.OrderByDescending(i => i.Severity).ThenBy(i => i.Kind).ThenBy(i => i.Id))
        {
            sb.AppendLine(i.ToString());
        }
        sb.Append($"{ErrorCount} error(s), {WarningCount} warning(s)");
        return sb.ToString();
    }
}
