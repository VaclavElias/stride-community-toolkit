using Stride.CommunityToolkit.Box2D;
using Stride.Core.Mathematics;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Box2D;

/// <summary>
/// Pins <see cref="SvgPath2D.Parse"/>: absolute and relative straight-line commands, the implicit
/// line-to after a move-to, offset and scale with the y flip, exponents, reversal, and the refusal
/// of curves. Pure string work, no world needed.
/// </summary>
public class SvgPath2DTests
{
    [Fact]
    public void Parse_AbsoluteCommands_FlipY()
    {
        var points = SvgPath2D.Parse("M 0,0 H 10 V 10 H 0 z");

        Assert.Equal([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, -10), new Vector2(0, -10)], points);
    }

    [Fact]
    public void Parse_RelativeCommands_MatchAbsolute()
    {
        var relative = SvgPath2D.Parse("m 1,2 h 3 v 4 l -3,0");
        var absolute = SvgPath2D.Parse("M 1,2 H 4 V 6 L 1,6");

        Assert.Equal(absolute, relative);
    }

    [Fact]
    public void Parse_CoordinatesAfterMoveTo_AreLineTos()
    {
        var points = SvgPath2D.Parse("M 0,0 1,1 2,0");

        Assert.Equal(3, points.Length);
        Assert.Equal(new Vector2(2, 0), points[2]);
    }

    [Fact]
    public void Parse_AppliesOffsetThenScale()
    {
        var points = SvgPath2D.Parse("M 10,20 L 12,21", offset: new Vector2(-10, -20), scale: 2);

        Assert.Equal(new Vector2(0, 0), points[0]);
        Assert.Equal(new Vector2(4, -2), points[1]);
    }

    [Fact]
    public void Parse_ReadsExponents_AsTheSamplePathsUseThem()
    {
        var points = SvgPath2D.Parse("M 0,0 h -10e-8 l 1,-1e-5");

        Assert.Equal(3, points.Length);
        Assert.Equal(0, points[1].X, 6);
        Assert.Equal(1, points[2].X, 6);
        Assert.Equal(0.00001f, points[2].Y, 6);
    }

    [Fact]
    public void Parse_Reverse_FlipsOrder()
    {
        var forward = SvgPath2D.Parse("M 0,0 H 1 V 1");
        var reversed = SvgPath2D.Parse("M 0,0 H 1 V 1", reverse: true);

        Assert.Equal(forward.Reverse(), reversed);
    }

    [Fact]
    public void Parse_Curve_IsRefusedWithTheCommandNamed()
    {
        var error = Assert.Throws<FormatException>(() => SvgPath2D.Parse("M 0,0 C 1,1 2,2 3,3"));

        Assert.Contains("'C'", error.Message);
    }

    [Fact]
    public void Parse_TheSamplesLevelPath_GivesAClosedOutline()
    {
        // The first ground of the samples' Mover scene, unchanged.
        const string path =
            "M 2.6458333,201.08333 H 293.68751 v -47.625 h -2.64584 l -10.58333,7.9375 -13.22916,7.9375 -13.24648,5.29167 "
            + "-31.73269,7.9375 -21.16667,2.64583 -23.8125,10.58333 H 142.875 v -5.29167 h -5.29166 v 5.29167 H 119.0625 v "
            + "-2.64583 h -2.64583 v -2.64584 h -2.64584 v -2.64583 H 111.125 v -2.64583 H 84.666668 v -2.64583 h -5.291666 v "
            + "-2.64584 h -5.291667 v -2.64583 H 68.791668 V 174.625 h -5.291666 v -2.64584 H 52.916669 L 39.6875,177.27083 H "
            + "34.395833 L 23.8125,185.20833 H 15.875 L 5.2916669,187.85416 V 153.45833 H 2.6458333 v 47.625";

        var points = SvgPath2D.Parse(path, new Vector2(-50, -200), 0.2f);

        Assert.True(points.Length > 40);
        Assert.Equal(points[0], points[^1]);          // the path returns to its start
        Assert.All(points, p => Assert.InRange(p.Y, -1f, 12f));
    }
}
