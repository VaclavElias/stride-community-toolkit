using Stride.CommunityToolkit.Charts.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering.Materials;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// One curve on a <see cref="Chart"/>: the entity that draws it, the options it was drawn with, and the GPU
/// buffers behind it. Take it off the chart with <see cref="Chart.Remove"/>, which detaches the entity and
/// disposes the buffers, or <see cref="Chart.Clear"/> for every curve at once.
/// </summary>
/// <remarks>
/// A ribbon's vertex and index buffers are created by <see cref="PolylineMeshBuilder"/> and tracked by
/// nothing else, so a curve that is merely dropped from the scene keeps its GPU memory until the game exits.
/// The series is the handle that knows what to free. <see cref="ChartTrajectory"/> derives from this for
/// curves that grow while the game runs.
/// </remarks>
public class ChartSeries : IDisposable
{
    /// <summary>The name given when the curve was plotted; also the entity's name.</summary>
    public string Name { get; }

    /// <summary>The entity drawing the curve, parented to the chart's <see cref="Chart.Root"/>.</summary>
    public Entity Entity { get; }

    /// <summary>Width, colour and glow the curve was drawn with, resolved from the style and the chart's defaults.</summary>
    internal PolylineOptions Options { get; }

    /// <summary>The curve's colour - what a legend would show next to <see cref="Name"/>.</summary>
    public Color Color => Options.Color;

    /// <summary>
    /// <see langword="true"/> when nothing of the curve fell inside the chart - every sample was out of range
    /// or not finite - so there is no mesh to draw. The entity still exists so the series can be handled like
    /// any other.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>Whether <see cref="Dispose()"/> has run.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Set for lines and parametric curves so a view-driven chart can re-clip them when the range changes.</summary>
    internal IReadOnlyList<Vector3>? SourcePoints { get; set; }

    /// <summary>Whether <see cref="SourcePoints"/> are clipped to the chart's ranges when re-plotted.</summary>
    internal bool ClipSource { get; set; }

    /// <summary>Set for scatter series so a view-driven chart can re-filter and re-size the markers.</summary>
    internal IReadOnlyList<Vector3>? MarkerPoints { get; set; }

    /// <summary>The marker's diagonal extent in chart units at the creation-time view.</summary>
    internal float MarkerSize { private get; set; }

    internal ChartSeries(string name, Entity entity, PolylineOptions options, bool isEmpty)
    {
        Name = name;
        Entity = entity;
        Options = options;
        IsEmpty = isEmpty;
    }

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
    /// Releases the curve's GPU buffers and removes its model, so the entity draws nothing. The entity is
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
    /// Rebuilds the scatter mesh from <see cref="MarkerPoints"/> for the chart's current ranges and view
    /// scale: one × per visible point, two ribbon segments each, all batched into a single mesh.
    /// </summary>
    internal void RebuildMarkerModel(Chart chart)
    {
        ReleaseModel();

        var half = MarkerSize * 0.5f * chart.ViewScale;
        var segments = CollectMarkerSegments(chart.Options, chart.Is3D, half);

        if (segments.Count == 0)
            return;

        var effective = chart.ScaledOptions(Options);
        var mesh = PolylineMeshBuilder.BuildSegments(chart.Game.GraphicsDevice, segments, effective);
        Entity.Add(PolylineExtensions.CreateModel(chart.Game, mesh, effective));
    }

    /// <summary>
    /// Releases the current model's mesh buffers and removes the model, ready for a rebuild.
    /// </summary>
    internal void ReleaseModel()
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
    /// Frees whatever GPU resources back the curve. The base implementation releases the buffers of every
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

    private List<(Vector3 Start, Vector3 End)> CollectMarkerSegments(ChartOptions o, bool is3D, float half)
    {
        var segments = new List<(Vector3 Start, Vector3 End)>();

        foreach (var p in MarkerPoints!)
        {
            // Only finite points inside the chart's ranges get a marker; the rest simply disappear until
            // panning or zooming brings them back
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z))
                continue;

            if (p.X < o.Range.XMin || p.X > o.Range.XMax || p.Y < o.Range.YMin || p.Y > o.Range.YMax)
                continue;

            if (is3D && (p.Z < o.Range.ZMin || p.Z > o.Range.ZMax))
                continue;

            segments.Add((new Vector3(p.X - half, p.Y - half, p.Z), new Vector3(p.X + half, p.Y + half, p.Z)));
            segments.Add((new Vector3(p.X - half, p.Y + half, p.Z), new Vector3(p.X + half, p.Y - half, p.Z)));
        }

        return segments;
    }
}