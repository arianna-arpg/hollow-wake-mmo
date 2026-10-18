namespace HollowWake.Core.Definitions;

/// <summary>Authored effective material density in kg per cubic world unit.</summary>
public sealed record BodyMaterialDef : Definition
{
    public double Density { get; init; }
}

public sealed record BodyMaterialShare
{
    public required string Material { get; init; }
    public double Fraction { get; init; } = 1;
}

/// <summary>Body-space envelope: width across the shoulders, depth forward, height up.</summary>
public sealed record PhysicalBodySpec
{
    public double Width { get; init; }
    public double Depth { get; init; }
    public double Height { get; init; }
    public double Fill { get; init; } = 1;
    public double Heft { get; init; } = 1;
    public List<BodyMaterialShare> Materials { get; init; } = new();
}
