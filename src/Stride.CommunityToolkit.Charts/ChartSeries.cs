using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering.Materials;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// One series on a <see cref="Chart"/>: the entity that draws it, the options it was drawn with, and the
/// GPU buffers behind it. Take it off the chart with <see cref="Chart.Remove"/>, which detaches the entity
/// and disposes the buffers, or <see cref="Chart.Clear"/> for every series at once.
/// </summary>
/// <remarks>
/// <para>
/// Each kind of series knows how to rebuild itself for the chart's current ranges - a
/// <see cref="ChartCurve"/> re-samples its function, a <see cref="ChartLineSeries"/> re-clips its points,
/// a <see cref="ChartAreaSeries"/> re-samples its bounds, a <see cref="ChartTrajectory"/> keeps its
/// recorded geometry and only rescales its width, and <see cref="ChartMarkerSeries"/> has nothing to
/// rebuild because it is drawn afresh each frame. The chart asks every series to do so when the range
/// changes, which is how a view-driven chart follows the camera.
/// </para>
/// <para>
/// A ribbon's vertex and index buffers are created by <see cref="PolylineMeshBuilder"/> and tracked by
/// nothing else, so a series that is merely dropped from the scene keeps its GPU memory until the game
/// exits. The series is the handle that knows what to free.
/// </para>
/// </remarks>
public abstract class ChartSeries : IDisposable
{
    /// <summary>The name given when the series was added; also the entity's name.</summary>
    public string Name { get; }

    /// <summary>The entity drawing the series, parented to the chart's <see cref="Chart.Root"/>.</summary>
    public Entity Entity { get; }

    /// <summary>Width, colour and glow the series was drawn with, resolved from the style and the chart's defaults.</summary>
    internal PolylineOptions Options { get; }

    /// <summary>The series' colour - what the legend shows next to <see cref="Name"/>.</summary>
    public Color Color => Options.Color;

    /// <summary>
    /// <see langword="true"/> when nothing of the series falls inside the chart's current ranges - every
    /// sample out of range or not finite - so there is nothing to draw. Refreshed whenever the series is
    /// rebuilt; the entity exists either way, so the series is handled like any other.
    /// </summary>
    public bool IsEmpty { get; private protected set; }

    /// <summary>Whether <see cref="Dispose()"/> has run.</summary>
    public bool IsDisposed { get; private set; }

    private protected ChartSeries(string name, Entity entity, PolylineOptions options)
    {
        Name = name;
        Entity = entity;
        Options = options;
    }

    /// <summary>
    /// Rebuilds the series for the chart's current ranges and view scale. Called once when the series is
    /// added and again whenever the range changes.
    /// </summary>
    internal abstract void Rebuild(Chart chart);

    /// <summary>
    /// Pushes a new emissive intensity into this series' material. Each series builds its own material, so
    /// this touches nothing else on the chart, and it is a parameter write rather than a rebuild.
    /// </summary>
    internal void SetEmissiveIntensity(float intensity)
    {
        if (Entity.Get<ModelComponent>()?.Model is not { } model)
            return;

        foreach (var instance in model.Materials)
        {
            instance.Material?.Passes[0].Parameters.Set(MaterialKeys.EmissiveIntensity, intensity);
        }
    }

    /// <summary>
    /// Replaces the series' model with ribbons through <paramref name="runs"/>, at the width the current
    /// view asks for, and records whether anything was drawn. What a curve, a line and a parametric plot
    /// all end with.
    /// </summary>
    /// <param name="chart">The chart, for its device and view scale.</param>
    /// <param name="runs">The clipped runs to draw; none leaves the series empty.</param>
    /// <param name="closed">Whether a single run should be joined back on itself.</param>
    private protected void ReplaceRibbons(Chart chart, List<IReadOnlyList<Vector3>> runs, bool closed)
    {
        ReleaseModel();

        IsEmpty = runs.Count == 0;

        if (IsEmpty)
            return;

        // A copy with the width scaled for the current view and the closure decided above; Options itself
        // is untouched so every re-plot scales from the intended base width
        var effective = new PolylineOptions
        {
            Width = Options.Width * chart.ViewScale,
            Color = Options.Color,
            EmissiveIntensity = Options.EmissiveIntensity,
            Normal = Options.Normal,
            Closed = closed,
        };

        var mesh = closed && runs.Count == 1
            ? PolylineMeshBuilder.Build(chart.Game.GraphicsDevice, runs[0], effective)
            : PolylineMeshBuilder.BuildMany(chart.Game.GraphicsDevice, runs, effective);

        Entity.Add(PolylineExtensions.CreateModel(chart.Game, mesh, effective));
    }

    /// <summary>
    /// Releases the current model's mesh buffers and removes the model, ready for a rebuild.
    /// </summary>
    private protected void ReleaseModel()
    {
        if (Entity.Get<ModelComponent>() is not { } old)
            return;

        if (old.Model is { } model)
        {
            foreach (var mesh in model.Meshes)
            {
                PolylineMeshBuilder.Release(mesh);
            }
        }

        Entity.Remove(old);
    }

    /// <summary>
    /// Releases the series' GPU buffers and removes its model, so the entity draws nothing. The entity is
    /// left where it is; <see cref="Chart.Remove"/> is what detaches it from the chart.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>The standard dispose pattern for an unsealed type; the work happens once, on the disposing path.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed || !disposing)
            return;

        IsDisposed = true;

        ReleaseResources();

        if (Entity.Get<ModelComponent>() is { } model)
        {
            Entity.Remove(model);
        }
    }

    /// <summary>
    /// Frees whatever GPU resources back the series. The base implementation releases the buffers of every
    /// mesh in the entity's model; <see cref="ChartTrajectory"/> disposes its growing buffers instead.
    /// </summary>
    private protected virtual void ReleaseResources()
    {
        var model = Entity.Get<ModelComponent>();

        if (model?.Model is null)
            return;

        foreach (var mesh in model.Model.Meshes)
        {
            PolylineMeshBuilder.Release(mesh);
        }
    }
}