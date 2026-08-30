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
/// The series is the handle that knows what to free.
/// </remarks>
public sealed class ChartSeries : IDisposable
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

    internal ChartSeries(string name, Entity entity, PolylineOptions options, bool isEmpty)
    {
        Name = name;
        Entity = entity;
        Options = options;
        IsEmpty = isEmpty;
    }

    /// <summary>
    /// Releases the ribbon's GPU buffers and removes its model, so the entity draws nothing. The entity is
    /// left where it is; <see cref="Chart.Remove"/> is what detaches it from the chart.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        var model = Entity.Get<ModelComponent>();

        if (model?.Model is null)
            return;

        foreach (var mesh in model.Model.Meshes)
        {
            PolylineMeshBuilder.Release(mesh);
        }

        Entity.Remove(model);
    }
}