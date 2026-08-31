using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The pure mathematics behind <c>Chart.FrameCamera</c>: how large an orthographic view, or how distant a
/// perspective camera, must be for a box to fit the window with some breathing room. Kept free of engine
/// state so it is covered by unit tests.
/// </summary>
public static class ChartFraming
{
    /// <summary>
    /// The orthographic size (the visible world height) at which a <paramref name="width"/> ×
    /// <paramref name="height"/> rectangle fits a window of the given <paramref name="aspectRatio"/> with
    /// <paramref name="padding"/> of extra room on every side.
    /// </summary>
    /// <param name="width">The rectangle's width in world units.</param>
    /// <param name="height">The rectangle's height in world units.</param>
    /// <param name="aspectRatio">The window's width over its height.</param>
    /// <param name="padding">Extra room as a fraction of the rectangle per side; <c>0.05</c> adds 5 % all round.</param>
    /// <returns>The orthographic size to set - whichever of the two axes needs more.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If a size, the aspect ratio, or the padding is not positive where it must be.</exception>
    public static float OrthographicSize(float width, float height, float aspectRatio, float padding = 0.05f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspectRatio);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);

        var padded = 1f + 2f * padding;

        return MathF.Max(height * padded, width * padded / aspectRatio);
    }

    /// <summary>
    /// The distance from the centre of <paramref name="box"/> at which a perspective camera looking along
    /// <paramref name="forward"/> sees the whole box with <paramref name="padding"/> of extra room. Every
    /// corner is projected onto the camera's own axes, so an oblique view fits as tightly as a head-on one.
    /// </summary>
    /// <param name="box">The world-space box to fit.</param>
    /// <param name="right">The camera's right axis, unit length.</param>
    /// <param name="up">The camera's up axis, unit length.</param>
    /// <param name="forward">The camera's viewing direction, unit length.</param>
    /// <param name="aspectRatio">The window's width over its height.</param>
    /// <param name="verticalFovRadians">The camera's vertical field of view in radians.</param>
    /// <param name="padding">Extra room as a fraction of the box per side; <c>0.05</c> adds 5 % all round.</param>
    /// <returns>The camera's distance from the box centre, along the opposite of <paramref name="forward"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the aspect ratio, the field of view, or the padding is out of range.</exception>
    public static float PerspectiveDistance(in BoundingBox box, Vector3 right, Vector3 up, Vector3 forward, float aspectRatio, float verticalFovRadians, float padding = 0.05f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspectRatio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(verticalFovRadians);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(verticalFovRadians, MathF.PI);
        ArgumentOutOfRangeException.ThrowIfNegative(padding);

        var centre = (box.Minimum + box.Maximum) * 0.5f;
        var tanVertical = MathF.Tan(verticalFovRadians * 0.5f);
        var tanHorizontal = tanVertical * aspectRatio;
        var padded = 1f + 2f * padding;

        var distance = 0f;

        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? box.Minimum.X : box.Maximum.X,
                (i & 2) == 0 ? box.Minimum.Y : box.Maximum.Y,
                (i & 4) == 0 ? box.Minimum.Z : box.Maximum.Z) - centre;

            // What this corner demands: its depth towards the camera, plus how far the frustum must
            // recede for the corner's lateral offset to fit inside the half angle
            var lateralX = MathF.Abs(Vector3.Dot(corner, right)) * padded;
            var lateralY = MathF.Abs(Vector3.Dot(corner, up)) * padded;
            var towardCamera = -Vector3.Dot(corner, forward);

            distance = MathF.Max(distance, towardCamera + lateralY / tanVertical);
            distance = MathF.Max(distance, towardCamera + lateralX / tanHorizontal);
        }

        return distance;
    }

    /// <summary>
    /// The axis-aligned box that encloses the given local box after transforming its eight corners - how a
    /// chart's ranges become world bounds when its root is moved, rotated or scaled.
    /// </summary>
    internal static BoundingBox TransformBox(Vector3 localMin, Vector3 localMax, in Matrix world)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? localMin.X : localMax.X,
                (i & 2) == 0 ? localMin.Y : localMax.Y,
                (i & 4) == 0 ? localMin.Z : localMax.Z);

            var transformed = Vector3.TransformCoordinate(corner, world);
            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }

        return new BoundingBox(min, max);
    }
}
