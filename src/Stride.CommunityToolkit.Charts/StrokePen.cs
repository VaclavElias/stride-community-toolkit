using Stride.CommunityToolkit.Shapes;
using Stride.Core.Mathematics;
using System.Buffers;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// Draws one series' runs as pixel-width strokes in one plane of the chart, with the series' glow set on
/// the batch for as long as the pen is held and put back when it is disposed. The batch may be the
/// caller's, so nothing about it is left changed.
/// </summary>
/// <remarks>
/// Runs are chart-local; a root drawn at some other scale than one has every point scaled into the
/// plane through a pooled scratch buffer, since the plane's axes are unit length whatever the scale.
/// </remarks>
internal ref struct StrokePen : IDisposable
{
    private readonly ShapeBatch _batch;
    private readonly ChartPlane _plane;
    private readonly ChartView _view;
    private readonly float _width;
    private readonly Color _color;
    private readonly float _savedGlow;
    private readonly Color? _savedGlowColor;
    private readonly bool _savedAdditive;
    private Vector2[]? _scratch;

    /// <summary>Sets the batch's glow to the series' and remembers the old one.</summary>
    /// <param name="batch">The batch to draw into.</param>
    /// <param name="view">This frame's view of the chart.</param>
    /// <param name="z">The chart-local depth of the plane the strokes lie in.</param>
    /// <param name="series">Whose width, colour and glow the strokes take.</param>
    internal StrokePen(ShapeBatch batch, in ChartView view, float z, ChartSeries series)
    {
        _batch = batch;
        _view = view;
        _plane = view.PlaneAt(z);
        _width = series.Width;
        _color = series.Color;

        _savedGlow = batch.Glow.Width;
        _savedGlowColor = batch.Glow.Color;
        _savedAdditive = batch.Glow.Additive;

        // The halo is the stroke's colour at a fraction of its strength, so it reads as light around the
        // line rather than as a wider line
        var o = series.Chart.Options.Series;
        var halo = new Color(_color.R, _color.G, _color.B, (byte)Math.Clamp((int)(o.GlowStrength * 255f), 0, 255));

        batch.Glow.Set(series.Glow, halo);
        batch.Glow.Additive = o.AdditiveGlow;
    }

    /// <summary>Submits one run; fewer than two points draw nothing.</summary>
    internal void Draw(ReadOnlySpan<Vector2> run, bool closed = false)
    {
        if (run.Length < 2)
            return;

        if (_view.IsUnitScale)
        {
            _batch.DrawPixelPolyline(run, _plane.Position, _plane.AxisX, _plane.AxisY, _width, _color, closed);

            return;
        }

        if (_scratch is null || _scratch.Length < run.Length)
        {
            if (_scratch is not null)
            {
                ArrayPool<Vector2>.Shared.Return(_scratch);
            }

            _scratch = ArrayPool<Vector2>.Shared.Rent(run.Length);
        }

        for (var i = 0; i < run.Length; i++)
        {
            _scratch[i] = _view.ToPlane(run[i]);
        }

        _batch.DrawPixelPolyline(_scratch.AsSpan(0, run.Length), _plane.Position, _plane.AxisX, _plane.AxisY, _width, _color, closed);
    }

    /// <summary>Puts the batch's glow back and returns the scratch buffer.</summary>
    public void Dispose()
    {
        _batch.Glow.Set(_savedGlow, _savedGlowColor);
        _batch.Glow.Additive = _savedAdditive;

        if (_scratch is not null)
        {
            ArrayPool<Vector2>.Shared.Return(_scratch);
            _scratch = null;
        }
    }
}