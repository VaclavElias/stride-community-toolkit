using Stride.CommunityToolkit.Rendering.Lines;
using Stride.Core.Mathematics;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Rendering;

/// <summary>
/// Pins what <see cref="PolylineClipping"/> does to the awkward curves a plotter meets: samples outside the
/// visible range, <c>NaN</c> from a function outside its domain, and the jump across an asymptote. All of it
/// is point arithmetic, so no graphics device is involved.
/// </summary>
public class PolylineClippingTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Clip_KeepsALineThatIsEntirelyInside_AsOneUnchangedRun()
    {
        Vector3[] points = [new(-1, -1, 0), new(0, 0.5f, 0), new(1, 1, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -5, 5);

        var run = Assert.Single(runs);
        Assert.Equal(points, run);
    }

    [Fact]
    public void Clip_EndsARunOnTheEdge_WhenTheLineLeaves()
    {
        // Rises out of the top of a [-1, 1] window between the second and third point
        Vector3[] points = [new(0, 0, 0), new(1, 0.5f, 0), new(2, 1.5f, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -1, 1);

        var run = Assert.Single(runs);
        Assert.Equal(3, run.Length);
        Assert.Equal(1f, run[^1].Y, Tolerance);
        Assert.Equal(1.5f, run[^1].X, Tolerance); // halfway along the segment, where y crosses 1
    }

    [Fact]
    public void Clip_SplitsIntoTwoRuns_WhenTheLineLeavesAndComesBack()
    {
        // A "V" that dips below the window: out through the bottom and back in
        Vector3[] points = [new(-2, 0, 0), new(0, -2, 0), new(2, 0, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -1, 1);

        Assert.Equal(2, runs.Count);
        Assert.Equal(-1f, runs[0][^1].Y, Tolerance);
        Assert.Equal(-1f, runs[1][0].Y, Tolerance);
        Assert.Equal(-1f, runs[0][^1].X, Tolerance);
        Assert.Equal(1f, runs[1][0].X, Tolerance);
    }

    [Fact]
    public void Clip_KeepsTheCrossingPiece_WhenBothEndsAreOutside()
    {
        Vector3[] points = [new(-10, 0, 0), new(10, 0, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -5, 5);

        var run = Assert.Single(runs);
        Assert.Equal(new Vector3(-5, 0, 0), run[0]);
        Assert.Equal(new Vector3(5, 0, 0), run[1]);
    }

    [Fact]
    public void Clip_DropsALineThatMissesTheRectangle()
    {
        Vector3[] points = [new(-10, 10, 0), new(10, 10, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -5, 5);

        Assert.Empty(runs);
    }

    [Fact]
    public void Clip_InterpolatesZ_AlongWithXAndY()
    {
        Vector3[] points = [new(-10, 0, 0), new(10, 0, 4)];

        var runs = PolylineClipping.Clip(points, -5, 5, -5, 5);

        var run = Assert.Single(runs);
        Assert.Equal(1f, run[0].Z, Tolerance);
        Assert.Equal(3f, run[1].Z, Tolerance);
    }

    [Fact]
    public void Clip_BreaksTheLine_AtANonFinitePoint()
    {
        Vector3[] points = [new(-1, 0, 0), new(0, float.NaN, 0), new(1, 0, 0), new(2, 0, 0)];

        var runs = PolylineClipping.Clip(points, -5, 5, -5, 5);

        var run = Assert.Single(runs);
        Assert.Equal(new Vector3(1, 0, 0), run[0]);
        Assert.Equal(new Vector3(2, 0, 0), run[1]);
    }

    [Fact]
    public void Clip_ReturnsNothing_ForFewerThanTwoPoints()
    {
        Assert.Empty(PolylineClipping.Clip([], -5, 5, -5, 5));
        Assert.Empty(PolylineClipping.Clip([new Vector3(0, 0, 0)], -5, 5, -5, 5));
    }

    [Fact]
    public void Clip_Throws_WhenTheRectangleIsInsideOut()
    {
        Assert.Throws<ArgumentException>(() => PolylineClipping.Clip([new Vector3(0, 0, 0)], 5, -5, -5, 5));
    }

    [Fact]
    public void SplitAtNonFinite_SeparatesTheDomainOfLog_FromTheNaNsBeforeIt()
    {
        var points = PolylineSampling.Function(x => MathF.Log(x), -2, 2, 9);

        var runs = PolylineClipping.SplitAtNonFinite(points);

        var run = Assert.Single(runs);
        Assert.All(run, p => Assert.True(p.X > 0f));
        Assert.All(run, p => Assert.True(float.IsFinite(p.Y)));
    }

    [Fact]
    public void SplitAtJumps_CutsTanAtEveryAsymptote()
    {
        // 4 asymptotes in [-5, 5] (at ±π/2 and ±3π/2) give 5 continuous branches
        var points = PolylineSampling.Function(MathF.Tan, -5, 5, 400);

        var runs = PolylineClipping.SplitAtJumps(points, maxJump: 8f);

        Assert.Equal(5, runs.Count);

        foreach (var run in runs)
        {
            for (var i = 1; i < run.Length; i++)
            {
                Assert.True(MathF.Abs(run[i].Y - run[i - 1].Y) <= 8f);
            }
        }
    }

    [Fact]
    public void SplitAtJumps_LeavesASmoothCurveAlone()
    {
        var points = PolylineSampling.Function(MathF.Sin, -5, 5, 100);

        var runs = PolylineClipping.SplitAtJumps(points, maxJump: 8f);

        var run = Assert.Single(runs);
        Assert.Equal(points.Length, run.Length);
    }

    [Fact]
    public void Append_DropsAPointThatRepeatsThePreviousOne()
    {
        Vector3[] points = [new(0, 0, 0), new(0, 0, 0), new(1, 1, 0)];

        var runs = PolylineClipping.SplitAtNonFinite(points);

        var run = Assert.Single(runs);
        Assert.Equal(2, run.Length);
    }
}
