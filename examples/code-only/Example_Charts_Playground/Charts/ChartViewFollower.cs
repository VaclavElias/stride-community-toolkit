using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// Keeps a chart's ranges in step with what an orthographic camera sees, turning it into a Desmos-style
/// infinite chart: pan and the axes follow, zoom out and the grid still covers the whole screen with a
/// coarser tick step, zoom in and it refines. Created with <see cref="Chart.FollowCamera"/>; call
/// <see cref="Update"/> every frame.
/// </summary>
/// <remarks>
/// The follower only reads the camera - position and orthographic size - and never writes to it, so it
/// works with <c>Basic2DCameraController</c>, any other controller, or a camera animated by hand. On each
/// meaningful view change it picks a 1-2-5 tick step for the zoom level (<see cref="Chart.NiceTickStep"/>)
/// and calls <see cref="Chart.SetVisibleRange"/>, which rebuilds the scaffolding and re-samples the
/// function plots. Changes smaller than half a percent of the view are ignored, so an idle camera costs
/// nothing.
/// </remarks>
public sealed class ChartViewFollower
{
    private readonly Game _game;
    private readonly Chart _chart;
    private Vector4? _lastRange;

    internal ChartViewFollower(Game game, Chart chart)
    {
        _game = game;
        _chart = chart;
    }

    /// <summary>
    /// Re-targets the chart to the camera's visible rectangle if the view has meaningfully changed.
    /// Does nothing for a perspective camera - the view-driven chart is a 2D, orthographic idea.
    /// </summary>
    /// <param name="camera">The camera whose view the chart should cover.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="camera"/> is <see langword="null"/>.</exception>
    public void Update(CameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (camera.Projection != CameraProjectionMode.Orthographic)
            return;

        var backBuffer = _game.GraphicsDevice.Presenter.BackBuffer;
        var aspect = (float)backBuffer.Width / backBuffer.Height;

        // The camera's position in chart space; the chart root may be moved
        var world = _chart.Root.Transform.WorldMatrix;
        Matrix.Invert(ref world, out var toChart);
        var centre = Vector3.TransformCoordinate(camera.Entity.Transform.WorldMatrix.TranslationVector, toChart);

        var height = camera.OrthographicSize;
        var width = height * aspect;

        var range = new Vector4(
            centre.X - width * 0.5f,
            centre.X + width * 0.5f,
            centre.Y - height * 0.5f,
            centre.Y + height * 0.5f);

        // Rebuilding the scaffolding is not free; ignore camera jitter below half a percent of the view
        if (_lastRange is { } last)
        {
            var threshold = height * 0.005f;

            if (MathF.Abs(range.X - last.X) < threshold && MathF.Abs(range.Y - last.Y) < threshold
                && MathF.Abs(range.Z - last.Z) < threshold && MathF.Abs(range.W - last.W) < threshold)
            {
                return;
            }
        }

        _lastRange = range;

        // One square grid step for both axes, refined or coarsened with the zoom; minors split a step of
        // 2 into quarters and steps of 1 and 5 into fifths, so minor lines land on readable values
        var step = Chart.NiceTickStep(height);
        var magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(step)));
        var mantissa = step / magnitude;

        var o = _chart.Options;
        o.TickStep = step;
        o.MinorDivisions = mantissa is > 1.5f and < 3f ? 4 : 5;

        _chart.SetVisibleRange(range.X, range.Y, range.Z, range.W);
    }
}
