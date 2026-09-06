using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// One frame's view of a chart: the root's world matrix and what one screen pixel measures in chart units
/// at any point of it. Built by <see cref="Chart.Update(CameraComponent)"/> and handed to everything that draws furniture,
/// so a tick length, a legend row or a marker asked for in pixels lands as the right number of chart units.
/// </summary>
/// <remarks>
/// The widths <see cref="Shapes.ShapeBatch"/> draws are measured per fragment and need none of this; it is the
/// <i>lengths</i> and <i>positions</i> laid out in pixels - how long a tick is, where the next legend row
/// sits - that have to be converted before they are submitted as world geometry. Under an orthographic
/// camera the answer is one number; under a perspective one it depends on how deep the point is, which is
/// why the conversion takes the point.
/// </remarks>
internal readonly struct ChartView
{
    private readonly bool _orthographic;
    private readonly float _orthographicSize;
    private readonly float _tanHalfFov;
    private readonly float _pixelHeight;
    private readonly float _pixelScale;
    private readonly float _rootScale;
    private readonly Vector3 _eye;
    private readonly Vector3 _forward;

    /// <summary>The chart root's world matrix, refreshed for this frame.</summary>
    internal Matrix World { get; }

    /// <param name="root">The chart's root entity; its world matrix is refreshed here.</param>
    /// <param name="camera">The camera looking at the chart.</param>
    /// <param name="pixelHeight">The back buffer height in physical pixels.</param>
    /// <param name="pixelScale">The display scale pixel sizes are multiplied by, or <c>1</c> for exact pixels.</param>
    internal ChartView(Entity root, CameraComponent camera, float pixelHeight, float pixelScale)
    {
        root.Transform.UpdateWorldMatrix();
        World = root.Transform.WorldMatrix;

        // A scaled root shrinks every chart unit on screen; the up vector's length is the Y scale, which
        // is the one the vertical pixel measure runs along
        _rootScale = MathF.Max(World.Up.Length(), MathUtil.ZeroTolerance);

        _pixelHeight = MathF.Max(pixelHeight, 1f);
        _pixelScale = pixelScale;

        var transform = camera.Entity.Transform;
        transform.UpdateWorldMatrix();
        _eye = transform.WorldMatrix.TranslationVector;
        _forward = transform.WorldMatrix.Forward;
        _forward.Normalize();

        _orthographic = camera.Projection == CameraProjectionMode.Orthographic;
        _orthographicSize = camera.OrthographicSize;
        _tanHalfFov = MathF.Tan(MathUtil.DegreesToRadians(camera.VerticalFieldOfView) * 0.5f);
    }

    /// <summary>The chart-local point in world space.</summary>
    internal Vector3 ToWorld(Vector3 local) => Vector3.TransformCoordinate(local, World);

    /// <summary>
    /// How many chart units one pixel spans at <paramref name="local"/>. Multiply a pixel length by it to
    /// get the chart units to lay out.
    /// </summary>
    internal float UnitsPerPixel(Vector3 local)
    {
        float worldPerPixel;

        if (_orthographic)
        {
            worldPerPixel = _orthographicSize / _pixelHeight;
        }
        else
        {
            // The visible height at the point's depth, spread over the window's pixels; a point at or
            // behind the eye gets a floor so nothing divides by zero
            var depth = MathF.Max(Vector3.Dot(ToWorld(local) - _eye, _forward), 1e-3f);
            worldPerPixel = 2f * depth * _tanHalfFov / _pixelHeight;
        }

        return worldPerPixel * _pixelScale / _rootScale;
    }

    /// <summary>A length in pixels as chart units at <paramref name="local"/>.</summary>
    internal float ToUnits(float pixels, Vector3 local) => pixels * UnitsPerPixel(local);
}