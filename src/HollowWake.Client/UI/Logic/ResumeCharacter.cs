namespace HollowWake.Client.UI.Logic;

public static class ResumeCharacter
{
    public static string? Select(IReadOnlyList<string> ids, string? remembered) =>
        remembered is not null && ids.Contains(remembered) ? remembered : ids.Count == 1 ? ids[0] : null;
}
