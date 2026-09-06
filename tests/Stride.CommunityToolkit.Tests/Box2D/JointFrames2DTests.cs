using Box2D.NET;
using Stride.CommunityToolkit.Box2D;
using Stride.Core.Mathematics;
using Xunit;
using static Box2D.NET.B2MathFunction;

namespace Stride.CommunityToolkit.Tests.Box2D;

/// <summary>
/// Pins the world-to-local frame arithmetic behind <see cref="Joints2D"/>, on bare transforms.
/// </summary>
public class JointFrames2DTests
{
    [Fact]
    public void LocalFrame_UndoesTheBodyTransform()
    {
        // Body at (10, 0), turned a quarter turn: its local x-axis points along world +y.
        var body = new B2Transform(new B2Vec2(10, 0), b2MakeRot(MathUtil.PiOverTwo));

        var frame = JointFrames2D.LocalFrame(body, new Vector2(10, 2));

        // Two units along world +y is two units along the body's local x.
        Assert.Equal(2, frame.p.X, 4);
        Assert.Equal(0, frame.p.Y, 4);

        // A world angle of zero, seen from a body turned +90 degrees, is -90 degrees.
        Assert.Equal(-MathUtil.PiOverTwo, b2Rot_GetAngle(frame.q), 4);
    }

    [Fact]
    public void LocalFrame_WithAWorldAngle_MakesTheFrameXAxisPointThatWay()
    {
        var body = new B2Transform(new B2Vec2(0, 0), b2MakeRot(0.3f));

        var frame = JointFrames2D.LocalFrame(body, Vector2.Zero, worldAngle: 1.0f);

        // body rotation * frame rotation == world angle
        // Box2D builds rotations from a fast cosine/sine approximation, hence the loose tolerance.
        Assert.Equal(1.0f, b2Rot_GetAngle(b2MulRot(body.q, frame.q)), 2);
    }

    [Fact]
    public void WorldPoint_RoundTripsALocalFrame()
    {
        var body = new B2Transform(new B2Vec2(-3, 4), b2MakeRot(2.1f));
        var pivot = new Vector2(1.5f, -2.5f);

        var frame = JointFrames2D.LocalFrame(body, pivot);
        var back = JointFrames2D.WorldPoint(body, frame);

        Assert.Equal(pivot.X, back.X, 4);
        Assert.Equal(pivot.Y, back.Y, 4);
    }

    [Fact]
    public void AxisAngle_IsTheDirectionOfTheAxis()
    {
        Assert.Equal(MathUtil.PiOverTwo, JointFrames2D.AxisAngle(new Vector2(0, 5)), 5);
        Assert.Equal(0, JointFrames2D.AxisAngle(Vector2.UnitX), 5);
        Assert.Throws<ArgumentException>(() => JointFrames2D.AxisAngle(Vector2.Zero));
    }
}
