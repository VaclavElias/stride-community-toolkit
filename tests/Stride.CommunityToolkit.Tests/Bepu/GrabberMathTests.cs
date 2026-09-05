using Stride.CommunityToolkit.Bepu;
using Stride.Core.Mathematics;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Bepu;

/// <summary>
/// Pins the arithmetic <see cref="GrabberScript"/> is built on: the grab point follows the body's
/// frame, the carry point rides the ray, and the force caps scale with mass and vanish for a body
/// that cannot be moved.
/// </summary>
public class GrabberMathTests
{
    [Fact]
    public void LocalGrabPoint_UndoesTheBodyPose()
    {
        // Body at (10, 0, 0), turned 90 degrees about Y: its local +X now points along world -Z.
        var position = new Vector3(10, 0, 0);
        var orientation = Quaternion.RotationY(MathUtil.PiOverTwo);
        var hit = position + Vector3.Transform(new Vector3(2, 1, 0), orientation);

        var local = GrabberMath.LocalGrabPoint(hit, position, orientation);

        Assert.Equal(2, local.X, 4);
        Assert.Equal(1, local.Y, 4);
        Assert.Equal(0, local.Z, 4);
    }

    [Fact]
    public void TargetPoint_IsTheDistanceAlongTheRay()
    {
        var target = GrabberMath.TargetPoint(new Vector3(1, 2, 3), Vector3.UnitZ, 4);

        Assert.Equal(new Vector3(1, 2, 7), target);
    }

    [Theory]
    [InlineData(1f, 360f)]        // 1 kg: 360 N
    [InlineData(0.01f, 36000f)]   // 100 kg: a hundred times the force, the same feel
    public void MaximumForce_ScalesWithMass(float inverseMass, float expected)
        => Assert.Equal(expected, GrabberMath.MaximumForce(360, inverseMass), 2);

    [Fact]
    public void MaximumForce_IsZeroForInfiniteMass()
        => Assert.Equal(0, GrabberMath.MaximumForce(360, 0));

    [Fact]
    public void MaximumTorque_GrowsWithTheLeverArm()
    {
        var near = GrabberMath.MaximumTorque(360, 0.5f, 1);
        var far = GrabberMath.MaximumTorque(360, 2f, 1);

        Assert.Equal(90, near, 2);
        Assert.Equal(360, far, 2);
    }
}