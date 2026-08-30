using Stride.CommunityToolkit.Rendering.Lines;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Extensions;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The chart's grid: one textured plane per requested coordinate plane and line weight, using the scene
/// editor's technique - the lines live in a mip-mapped texture filtered by the GPU sampler, so they are
/// stable at every zoom, and a range change only moves transforms instead of rebuilding geometry.
/// </summary>
internal sealed class ChartGrid : IDisposable
{
    private readonly Game _game;
    private readonly Chart _chart;
    private readonly List<Entry> _planes = [];
    private Texture? _texture;
    private bool _isDisposed;

    internal ChartGrid(Game game, Chart chart)
    {
        _game = game;
        _chart = chart;
    }

    /// <summary>Shows or hides the grid planes; the minor planes only show when there are minor divisions.</summary>
    internal bool Visible
    {
        get => _planes.Count > 0 && _planes[0].Model.Enabled;
        set
        {
            foreach (var entry in _planes)
            {
                entry.Model.Enabled = value && (!entry.IsMinor || _chart.Options.MinorDivisions > 1);
            }
        }
    }

    /// <summary>
    /// Creates the planes once: a flat chart draws only the chart plane, a 3D one draws whichever planes
    /// were asked for, one major and one minor plane each - the editor's grid gizmo runs up to three the
    /// same way.
    /// </summary>
    internal void Create()
    {
        var device = _game.GraphicsDevice;
        _texture = ChartGridTexture.Create(device);

        var planes = _chart.Is3D ? _chart.Options.GridPlanes : _chart.Options.GridPlanes & ChartGridPlanes.XY;

        foreach (var plane in new[] { ChartGridPlanes.XY, ChartGridPlanes.XZ, ChartGridPlanes.YZ })
        {
            if ((planes & plane) == 0)
                continue;

            Add(device, plane, isMinor: false, _chart.Options.GridColor, -Chart.LayerStep);
            Add(device, plane, isMinor: true, _chart.Options.MinorGridColor, -2f * Chart.LayerStep);
        }
    }

    /// <summary>
    /// Points every plane at the current ranges: each is scaled so one texture cell equals its step and
    /// snapped to a cell multiple near the view centre, so the grid appears infinite and its lines land
    /// exactly on the tick values.
    /// </summary>
    internal void Update()
    {
        var o = _chart.Options;
        var centre = new Vector3((o.XMin + o.XMax) * 0.5f, (o.YMin + o.YMax) * 0.5f, (o.ZMin + o.ZMax) * 0.5f);
        var anchor = new Vector3(
            Math.Clamp(0f, o.XMin, o.XMax),
            Math.Clamp(0f, o.YMin, o.YMax),
            _chart.Is3D ? Math.Clamp(0f, o.ZMin, o.ZMax) : 0f);

        var visible = Visible;

        foreach (var entry in _planes)
        {
            var cell = entry.IsMinor ? o.TickStep / Math.Max(1, o.MinorDivisions) : o.TickStep;

            entry.Model.Enabled = visible && (!entry.IsMinor || o.MinorDivisions > 1);
            entry.Entity.Transform.Scale = new Vector3(cell, cell, 1f);

            // Snap the two spanned coordinates to cell multiples; hold the third on its axis
            entry.Entity.Transform.Position = entry.Plane switch
            {
                ChartGridPlanes.XZ => new Vector3(Snap(centre.X, cell), anchor.Y + entry.Offset, Snap(centre.Z, cell)),
                ChartGridPlanes.YZ => new Vector3(anchor.X + entry.Offset, Snap(centre.Y, cell), Snap(centre.Z, cell)),
                _ => new Vector3(Snap(centre.X, cell), Snap(centre.Y, cell), anchor.Z + entry.Offset),
            };
        }

        static float Snap(float value, float cell) => MathF.Round(value / cell) * cell;
    }

    /// <summary>Removes the planes and frees their quad meshes and the shared grid texture.</summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        foreach (var entry in _planes)
        {
            if (entry.Model.Model is { } model)
            {
                foreach (var mesh in model.Meshes)
                {
                    PolylineMeshBuilder.Release(mesh);
                }
            }

            _chart.Root.RemoveChild(entry.Entity);
        }

        _planes.Clear();
        _texture?.Dispose();
        _texture = null;
    }

    private void Add(GraphicsDevice device, ChartGridPlanes plane, bool isMinor, Color color, float offset)
    {
        var material = ChartGridTexture.CreateMaterial(device, _texture!, color);

        var entity = new Entity($"{plane} {(isMinor ? "minor" : "major")} grid")
        {
            new ModelComponent
            {
                Model = new Model
                {
                    material,
                    new Mesh { Draw = GeometricPrimitive.Plane.New(device, ChartGridTexture.PlaneCells, ChartGridTexture.PlaneCells).ToMeshDraw() },
                },
            },
        };

        // The plane primitive lies in XY; rotate it onto the other coordinate planes
        entity.Transform.Rotation = plane switch
        {
            ChartGridPlanes.XZ => Quaternion.RotationX(MathUtil.PiOverTwo),
            ChartGridPlanes.YZ => Quaternion.RotationY(MathUtil.PiOverTwo),
            _ => Quaternion.Identity,
        };

        var model = entity.Get<ModelComponent>()!;
        model.Enabled = _chart.Options.GridVisible && (!isMinor || _chart.Options.MinorDivisions > 1);

        _planes.Add(new Entry(entity, model, plane, isMinor, offset));
        _chart.Root.AddChild(entity);
    }

    private sealed record Entry(Entity Entity, ModelComponent Model, ChartGridPlanes Plane, bool IsMinor, float Offset);
}