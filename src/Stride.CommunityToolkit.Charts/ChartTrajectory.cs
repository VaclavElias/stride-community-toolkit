using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A curve that grows one point at a time - the path of a moving body, drawn while it moves. Created with
/// <see cref="Chart.AddTrajectory"/>; feed it with <see cref="Add"/> every frame.
/// </summary>
/// <remarks>
/// Points are clipped to the chart's ranges the way <see cref="Chart.Plot"/> clips a function: a segment
/// that leaves the chart ends exactly on the edge, and when the path comes back the trail resumes from the
/// edge as a separate run. Behind it sits a <see cref="GrowingPolyline"/> whose GPU buffers are allocated
/// once, so adding a point per frame costs no allocations. Call <see cref="Add"/> from the game thread.
/// </remarks>
public sealed class ChartTrajectory : ChartSeries
{
    private readonly GrowingPolyline _line;
    private readonly ChartOptions _chartOptions;
    private Vector3? _previous;
    private bool _tipOnLine;

    /// <summary>How many points the trail currently holds.</summary>
    public int Count => _line.Count;

    /// <summary>The most points the trail can hold, set by <see cref="Chart.AddTrajectory"/>.</summary>
    public int Capacity => _line.Capacity;

    internal ChartTrajectory(string name, Entity entity, PolylineOptions options, GrowingPolyline line, ChartOptions chartOptions)
        : base(name, entity, options, isEmpty: false)
    {
        _line = line;
        _chartOptions = chartOptions;
    }

    /// <summary>
    /// Appends the body's current position to the trail. Positions outside the chart's ranges are clipped -
    /// the trail runs to the edge, pauses, and resumes where the path re-enters. A position that is not
    /// finite breaks the trail.
    /// </summary>
    /// <param name="point">The position, in chart units.</param>
    public void Add(Vector3 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
        {
            Break();
            return;
        }

        var previous = _previous;
        _previous = point;

        if (previous is null)
        {
            // No segment to draw yet; a lone inside point becomes the start of the first one
            if (IsInside(point))
            {
                _line.Add(point);
                _tipOnLine = true;
            }

            return;
        }

        var box = ClipBox();

        if (!PolylineClipping.ClipSegment(previous.Value, point, in box, out var t0, out var t1))
        {
            _line.Break();
            _tipOnLine = false;
            return;
        }

        if (t0 > 0f)
        {
            // Re-entering: resume from the edge as a new run
            _line.Break();
            _line.Add(Vector3.Lerp(previous.Value, point, t0));
        }
        else if (!_tipOnLine)
        {
            _line.Add(previous.Value);
        }

        _line.Add(Vector3.Lerp(previous.Value, point, t1));

        if (t1 < 1f)
        {
            // Leaving: the trail ends on the edge until the path comes back
            _line.Break();
            _tipOnLine = false;
        }
        else
        {
            _tipOnLine = true;
        }
    }

    /// <summary>
    /// Lifts the pen: the next <see cref="Add"/> starts a new run instead of connecting to the last position.
    /// </summary>
    public void Break()
    {
        _line.Break();
        _previous = null;
        _tipOnLine = false;
    }

    /// <summary>
    /// Removes every point, keeping the buffers - restart a throw, rewind a simulation.
    /// </summary>
    public void Clear()
    {
        _line.Clear();
        _previous = null;
        _tipOnLine = false;
    }

    /// <summary>
    /// Rescales the trail's ribbon width for the current view, keeping the recorded geometry - called by
    /// the chart when a view-driven range change would otherwise leave the trail too thick or too thin.
    /// </summary>
    internal void RescaleWidth(float scale) => _line.SetWidthScale(scale);

    /// <inheritdoc />
    private protected override void ReleaseResources() => _line.Dispose();

    /// <summary>The chart's ranges as a clip box; a flat chart just leaves Z unbounded.</summary>
    private BoundingBox ClipBox()
    {
        var o = _chartOptions;

        return new BoundingBox(
            new Vector3(o.XMin, o.YMin, o.ZMax > o.ZMin ? o.ZMin : -PolylineClipping.UnboundedZ),
            new Vector3(o.XMax, o.YMax, o.ZMax > o.ZMin ? o.ZMax : PolylineClipping.UnboundedZ));
    }

    private bool IsInside(Vector3 point)
    {
        var o = _chartOptions;

        return point.X >= o.XMin && point.X <= o.XMax && point.Y >= o.YMin && point.Y <= o.YMax
            && (o.ZMax <= o.ZMin || (point.Z >= o.ZMin && point.Z <= o.ZMax));
    }
}