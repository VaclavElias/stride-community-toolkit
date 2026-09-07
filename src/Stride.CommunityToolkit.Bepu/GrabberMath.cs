using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Bepu;

/// <summary>
/// The arithmetic behind <see cref="GrabberScript"/>, kept free of engine objects so it can be
/// tested on its own.
/// </summary>
internal static class GrabberMath
{
    /// <summary>
    /// The hit point expressed in the body's local frame, so the servo keeps pulling the same spot
    /// on the body however it turns.
    /// </summary>
    public static Vector3 LocalGrabPoint(Vector3 hitPoint, Vector3 bodyPosition, Quaternion bodyOrientation)
    {
        var inverse = bodyOrientation;
        inverse.Invert();

        return Vector3.Transform(hitPoint - bodyPosition, inverse);
    }

    /// <summary>
    /// Where the grab point is asked to be: a fixed distance along the pick ray, so the body rides
    /// the cursor and comes with the camera.
    /// </summary>
    public static Vector3 TargetPoint(Vector3 rayOrigin, Vector3 rayDirection, float distance)
        => rayOrigin + rayDirection * distance;

    /// <summary>
    /// The linear servo's force cap, scaled by mass so a heavy body is as draggable as a light one.
    /// Zero inverse mass (kinematic, or infinite mass) yields zero: nothing to grab.
    /// </summary>
    public static float MaximumForce(float forcePerKilogram, float inverseMass)
        => inverseMass > 0 ? forcePerKilogram / inverseMass : 0;

    /// <summary>
    /// The angular servo's torque cap: half the linear rate, times the grab point's lever arm,
    /// scaled by mass - the demo's proportions, which hold the orientation without fighting the
    /// linear pull.
    /// </summary>
    public static float MaximumTorque(float forcePerKilogram, float leverArm, float inverseMass)
        => inverseMass > 0 ? leverArm * forcePerKilogram * 0.5f / inverseMass : 0;
}