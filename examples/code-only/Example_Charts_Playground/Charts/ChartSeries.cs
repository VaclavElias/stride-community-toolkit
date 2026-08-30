using Stride.CommunityToolkit.Rendering.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;

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

    /// <summary>Width, colour and glow the curve was drawn with.</summary>
    public PolylineOptions Options { get; }

    /// <summary>The curve's colour - what a legend would show next to <see cref="Name"/>.</summary>
    public Color Color => Options.Color;

    /// <summary>
    /// <see langword="true"/> when nothing of the curve fell inside the chart - every sample was out of range
    /// or not finite - so there is no mesh to draw. The entity still exists so the series can be handled like
    /// any other.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    public bool IsDisposed { get; private set; }

    // Set for y = f(x) plots so a view-driven chart can re-sample the curve when the range changes
    internal Func<float, float>? Function;
    internal int SampleCount;
    internal float SampleDensity;

    // Set for lines and parametric curves so a view-driven chart can re-clip them when the range changes
    internal IReadOnlyList<Vector3>? SourcePoints;
    internal bool ClipSource;

    internal ChartSeries(string name, Entity entity, PolylineOptions options, bool isEmpty)
    {
        Name = name;
        Entity = entity;
        Options = options;
        IsEmpty = isEmpty;
    }

    /// <summary>
    /// Releases the curve's GPU buffers and removes its model, so the entity draws nothing. The entity is
    /// left where it is; <see cref="Chart.Remove"/> is what detaches it from the chart.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        ReleaseResources();

        if (Entity.Get<ModelComponent>() is { } model)
        {
            Entity.Remove(model);
        }
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
}
