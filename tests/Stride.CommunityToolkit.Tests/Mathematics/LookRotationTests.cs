using Stride.CommunityToolkit.Mathematics;
using Stride.Core.Mathematics;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Mathematics;

/// <summary>
/// Guards <see cref="MathUtilEx.LookRotation(Vector3, Vector3, Vector3)"/> against the degenerate
/// inputs that used to produce a non-finite quaternion.
/// </summary>
/// <remarks>
/// A NaN here does not stay local: assigning it to a <c>TransformComponent.Rotation</c> poisons the
/// entity's rotation matrix, and anything that then integrates a position through that matrix turns
/// the position into NaN too. In a camera that means a view that cannot be recovered except by
/// resetting the transform outright, which is how this was found.
/// </remarks>
public class LookRotationTests
{
    private static void AssertFinite(Quaternion rotation)
    {
        Assert.False(float.IsNaN(rotation.X) || float.IsInfinity(rotation.X), $"X was {rotation.X}");
        Assert.False(float.IsNaN(rotation.Y) || float.IsInfinity(rotation.Y), $"Y was {rotation.Y}");
        Assert.False(float.IsNaN(rotation.Z) || float.IsInfinity(rotation.Z), $"Z was {rotation.Z}");
        Assert.False(float.IsNaN(rotation.W) || float.IsInfinity(rotation.W), $"W was {rotation.W}");
    }

    private static void AssertUnitLength(Quaternion rotation)
        => Assert.Equal(1f, rotation.Length(), 3);

    /// <summary>
    /// The case that was actually hit in play: orbiting a camera to the far side of its target, so
    /// the look rotation is a 180 degree turn about Y. The rotation matrix trace is exactly -1 there,
    /// which the old single-branch conversion turned into a division by zero.
    /// </summary>
    [Fact]
    public void LookingFromBehindTheTargetIsFiniteAndUnit()
    {
        var rotation = MathUtilEx.LookRotation(new Vector3(0, 2.5f, -10), new Vector3(0, 2.5f, 0), Vector3.UnitY);

        AssertFinite(rotation);
        AssertUnitLength(rotation);
    }

    /// <summary>
    /// The same singularity swept through, because the exact angle is only reached when the frame
    /// timing happens to land on it. Every orbit angle has to be safe, not just the ones a given run
    /// samples.
    /// </summary>
    [Fact]
    public void EveryOrbitAngleIsFiniteAndUnit()
    {
        var target = new Vector3(0, 2.5f, 0);

        for (var degrees = 0; degrees < 360; degrees++)
        {
            var radians = MathUtil.DegreesToRadians(degrees);
            var eye = target + new Vector3(MathF.Sin(radians) * 10f, 4f, MathF.Cos(radians) * 10f);

            var rotation = MathUtilEx.LookRotation(eye, target, Vector3.UnitY);

            AssertFinite(rotation);
            AssertUnitLength(rotation);
        }
    }

    /// <summary>
    /// Looking straight down: the forward direction is parallel to the up vector, so the basis cannot
    /// be built the usual way and a fallback reference axis is needed.
    /// </summary>
    [Fact]
    public void LookingStraightDownIsFiniteAndUnit()
    {
        var rotation = MathUtilEx.LookRotation(new Vector3(0, 10, 0), Vector3.Zero, Vector3.UnitY);

        AssertFinite(rotation);
        AssertUnitLength(rotation);
    }

    /// <summary>
    /// Looking straight up, the same degeneracy with the opposite sign.
    /// </summary>
    [Fact]
    public void LookingStraightUpIsFiniteAndUnit()
    {
        var rotation = MathUtilEx.LookRotation(Vector3.Zero, new Vector3(0, 10, 0), Vector3.UnitY);

        AssertFinite(rotation);
        AssertUnitLength(rotation);
    }

    /// <summary>
    /// Eye and target in the same place. There is no direction to face, so the only requirement is
    /// that it does not produce a broken rotation.
    /// </summary>
    [Fact]
    public void EyeAtTargetIsFiniteAndUnit()
    {
        var rotation = MathUtilEx.LookRotation(new Vector3(3, 4, 5), new Vector3(3, 4, 5), Vector3.UnitY);

        AssertFinite(rotation);
        AssertUnitLength(rotation);
    }

    /// <summary>
    /// A zero up vector is a caller error rather than a geometric degeneracy, but it should still not
    /// hand back something that will poison a transform.
    /// </summary>
    [Fact]
    public void ZeroUpVectorIsFiniteAndUnit()
    {
        var rotation = MathUtilEx.LookRotation(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Zero);

        AssertFinite(rotation);
        AssertUnitLength(rotation);
    }

    /// <summary>
    /// The ordinary case still has to come out right. A camera on +Z looking at the origin is the
    /// identity rotation in Stride, which looks down -Z.
    /// </summary>
    [Fact]
    public void LookingDownNegativeZIsIdentity()
    {
        var rotation = MathUtilEx.LookRotation(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY);

        AssertFinite(rotation);
        AssertUnitLength(rotation);

        Assert.Equal(0f, rotation.X, 3);
        Assert.Equal(0f, rotation.Y, 3);
        Assert.Equal(0f, rotation.Z, 3);
        Assert.Equal(1f, MathF.Abs(rotation.W), 3);
    }

    /// <summary>
    /// The forward direction has to keep pointing at the target, which is the part a rewrite of the
    /// conversion could silently get wrong while still returning something finite.
    /// </summary>
    [Theory]
    [InlineData(0f, 0f, 10f)]
    [InlineData(10f, 0f, 0f)]
    [InlineData(-7f, 3f, 4f)]
    [InlineData(0f, -6f, 2f)]
    public void ForwardPointsAtTheTarget(float eyeX, float eyeY, float eyeZ)
    {
        var eye = new Vector3(eyeX, eyeY, eyeZ);
        var target = Vector3.Zero;

        var rotation = MathUtilEx.LookRotation(eye, target, Vector3.UnitY);

        // A Stride camera with no rotation looks down its local -Z, so the world-space view direction
        // is that axis put through the rotation
        var viewDirection = Vector3.Transform(-Vector3.UnitZ, rotation);
        var expected = Vector3.Normalize(target - eye);

        Assert.Equal(expected.X, viewDirection.X, 3);
        Assert.Equal(expected.Y, viewDirection.Y, 3);
        Assert.Equal(expected.Z, viewDirection.Z, 3);
    }
}
