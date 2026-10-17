namespace HollowWake.Server.Config;

/// <summary>
/// How snapshots trade precision and redundancy for bytes: the <c>snapshots:</c> section of
/// config/server.yaml. Every value has a default, documented with the rules it tunes in
/// docs/design/networking-and-persistence.md.
/// </summary>
public sealed record SnapshotSettings
{
    /// <summary>Default for <see cref="ResourceStep"/>.</summary>
    public const double DefaultResourceStep = 0.02;

    /// <summary>Default for <see cref="CastSteps"/>.</summary>
    public const int DefaultCastSteps = 5;

    public static readonly SnapshotSettings Default = new();

    /// <summary>How often an entity that changed is resent in full once it goes quiet, and static entities are re-verified.</summary>
    public double RefreshSeconds { get; init; } = 2;

    /// <summary>How often every known dynamic entity is resent in full whether or not it changed.</summary>
    public double SweepSeconds { get; init; } = 10;

    /// <summary>How often the self state is checked for change; also the step its cooldowns count down in.</summary>
    public double SelfSeconds { get; init; } = 0.5;

    /// <summary>The longest a client goes without a self state.</summary>
    public double SelfMaxSeconds { get; init; } = 2;

    /// <summary>
    /// A resource counts as changed when it moves by this fraction of its maximum, no finer than a tenth of a
    /// point and no coarser than this fraction of a hundred points.
    /// </summary>
    public double ResourceStep { get; init; } = DefaultResourceStep;

    /// <summary>Cast progress counts as changed in this many steps per cast.</summary>
    public int CastSteps { get; init; } = DefaultCastSteps;

    /// <summary>How far above and below a player interest reaches: interest is a column, not a sphere.</summary>
    public double InterestHeight { get; init; } = 64;

    /// <summary>The same settings with every value brought into a range the snapshot builder can use.</summary>
    public SnapshotSettings Normalized()
    {
        var refresh = Math.Max(0.05, RefreshSeconds);
        var self = Math.Max(0.05, SelfSeconds);
        return this with
        {
            RefreshSeconds = refresh,
            SweepSeconds = Math.Max(refresh, SweepSeconds),
            SelfSeconds = self,
            SelfMaxSeconds = Math.Max(self, SelfMaxSeconds),
            ResourceStep = Math.Max(0, ResourceStep),
            CastSteps = Math.Max(1, CastSteps),
            InterestHeight = Math.Max(0, InterestHeight),
        };
    }
}
