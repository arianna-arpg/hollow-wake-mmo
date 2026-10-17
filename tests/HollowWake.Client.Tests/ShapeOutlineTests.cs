using HollowWake.Client.UI.Logic;
using Xunit;

namespace HollowWake.Client.Tests;

public class ShapeOutlineTests
{
    private static ShapeRegion Region(string name, float? radius = null, float? angle = null, float? width = null, float? inner = null) =>
        ShapeOutline.RegionOf(ShapeOutline.SpecOf(name, radius, angle, width, inner, 1));

    [Fact]
    public void SizesTheWireLeavesOutTakeTheShapesDefaults()
    {
        var crescent = ShapeOutline.SpecOf("crescent", 4, null, null, null, 1);
        Assert.Equal(2, crescent.Inner, 3);
        Assert.Equal(MathF.PI, crescent.Angle, 3);
        Assert.Equal(3, ShapeOutline.SpecOf("band", 3, null, null, null, 1).Width, 3);
        Assert.Equal("circle", ShapeOutline.SpecOf("mystery", null, null, null, null, 2).Name);
        Assert.Equal(2, ShapeOutline.SpecOf("mystery", null, null, null, null, 2).Radius, 3);
    }

    [Fact]
    public void CenteredShapesSitOnTheAnchor()
    {
        var ring = Region("circle", 3, inner: 1);
        Assert.True(ShapeOutline.Inside(ring, 2, 0));
        Assert.False(ShapeOutline.Inside(ring, 0.5f, 0));
        Assert.False(ShapeOutline.Inside(ring, 3.2f, 0));
        var square = Region("square", 2);
        Assert.True(ShapeOutline.Inside(square, 1.9f, -1.9f));
        Assert.Equal(2 * MathF.Sqrt(2), ShapeOutline.Reach(square), 3);
        var triangle = Region("triangle", 2);
        Assert.True(ShapeOutline.Inside(triangle, 1.5f, 0));
        Assert.False(ShapeOutline.Inside(triangle, -1.5f, 0));
        var bar = Region("bar", 3, width: 1);
        Assert.True(ShapeOutline.Inside(bar, 0, 2.5f));
        Assert.False(ShapeOutline.Inside(bar, 1, 0));
    }

    [Fact]
    public void AnchoredShapesExtendAlongTheFacing()
    {
        var cone = Region("cone", 5, MathF.PI / 2);
        Assert.True(ShapeOutline.Inside(cone, 4, 0));
        Assert.True(ShapeOutline.Inside(cone, 3, 2.5f));
        Assert.False(ShapeOutline.Inside(cone, 1, 3));
        Assert.False(ShapeOutline.Inside(cone, -1, 0));
        var crescent = Region("crescent", 4);
        Assert.True(ShapeOutline.Inside(crescent, 3, 0));
        Assert.False(ShapeOutline.Inside(crescent, 1, 0));
        var line = Region("line", 6, width: 1, inner: 1);
        Assert.True(ShapeOutline.Inside(line, 5, 0.4f));
        Assert.False(ShapeOutline.Inside(line, 0.5f, 0));
        Assert.False(ShapeOutline.Inside(line, 5, 0.6f));
    }

    [Fact]
    public void ArcsAndWavesBowForward()
    {
        var arc = Region("arc", 3, MathF.PI / 2, 0.6f);
        Assert.True(ShapeOutline.Inside(arc, 0, 0));
        Assert.False(ShapeOutline.Inside(arc, -1, 0));
        var wave = Region("wave", 2, width: 0.5f);
        Assert.True(ShapeOutline.Inside(wave, 0, 0));
        Assert.True(ShapeOutline.Reach(wave) > 2);
    }
}
