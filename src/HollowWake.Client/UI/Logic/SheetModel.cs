using System.Globalization;

namespace HollowWake.Client.UI.Logic;

/// <summary>One row of the character sheet: a label, the value as the server sent it (formatted), and its description.</summary>
public sealed record SheetRow(string Id, string Label, string Value, string Description, string? Thread = null);

/// <summary>One section of the sheet with its rows in order.</summary>
public sealed record SheetSection(string Id, string Name, string Kind, List<SheetRow> Rows);

/// <summary>A quest as the log lists it.</summary>
public sealed record QuestLine(string Name, string State, IReadOnlyList<string> Objectives);

/// <summary>
/// Lays out the character sheet from the sheet content: which attributes in which order under which heading,
/// labelled and described by their definitions, with the values the server sent. It formats numbers for
/// reading (percentages, decimals) and never computes one.
/// </summary>
public static class SheetModel
{
    public static List<SheetSection> Build(
        IReadOnlyList<UiSheetSection> sections,
        ContentNames names,
        IReadOnlyDictionary<string, double> values,
        IReadOnlyList<QuestLine> quests)
    {
        var result = new List<SheetSection>(sections.Count);
        foreach (var section in sections)
        {
            var rows = new List<SheetRow>();
            switch (section.Kind)
            {
                case "attributes":
                    var listed = new HashSet<string>(StringComparer.Ordinal);
                    void Add(string id)
                    {
                        if (!listed.Add(id) || !values.TryGetValue(id, out var v))
                        {
                            return;
                        }
                        var info = names.AttributeById.GetValueOrDefault(id);
                        rows.Add(new SheetRow(id, info?.Name ?? ContentNames.Humanize(id), Format(v, info), info?.Description ?? ""));
                    }
                    foreach (var id in section.Attributes)
                    {
                        Add(id);
                    }
                    foreach (var info in names.Attributes)
                    {
                        if (info.Hidden || !section.Categories.Contains(info.Category) || section.Exclude.Any(p => info.Id.StartsWith(p, StringComparison.Ordinal)))
                        {
                            continue;
                        }
                        Add(info.Id);
                    }
                    break;
                case "threads":
                    if (section.Pattern is null)
                    {
                        break;
                    }
                    foreach (var thread in names.Threads)
                    {
                        var id = section.Pattern.Replace("{thread}", thread.Id, StringComparison.Ordinal);
                        if (!values.TryGetValue(id, out var v))
                        {
                            continue;
                        }
                        var info = names.AttributeById.GetValueOrDefault(id);
                        var text = Format(v, info);
                        if (section.Cap is not null)
                        {
                            var capId = section.Cap.Replace("{thread}", thread.Id, StringComparison.Ordinal);
                            if (values.TryGetValue(capId, out var cap))
                            {
                                text += $" (max {Format(cap, names.AttributeById.GetValueOrDefault(capId))})";
                            }
                        }
                        rows.Add(new SheetRow(id, thread.Name, text, thread.Description, thread.Id));
                    }
                    break;
                case "quests":
                    foreach (var q in quests)
                    {
                        rows.Add(new SheetRow(q.Name, q.Name, q.State, string.Join("\n", q.Objectives)));
                    }
                    break;
            }
            result.Add(new SheetSection(section.Id, section.Name, section.Kind, rows));
        }
        return result;
    }

    /// <summary>A value as the sheet reads it: percentages for percent attributes, the definition's decimals otherwise.</summary>
    public static string Format(double value, AttributeInfo? info)
    {
        if (info?.Percent == true)
        {
            return (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }
        var decimals = info?.Decimals ?? 2;
        var format = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
