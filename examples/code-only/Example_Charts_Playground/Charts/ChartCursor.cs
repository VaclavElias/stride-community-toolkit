using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Lines;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.Core.Mathematics;
using Stride.Engine;
using System.Globalization;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// A coordinate readout that follows the mouse: a small ring marker on the chart plane under the cursor and
/// a label with the chart-space coordinates next to it, hidden while the cursor is off the chart. Created
/// with <see cref="Chart.AddCursor"/>; call <see cref="Update"/> every frame with the camera and the mouse
/// position.
/// </summary>
/// <remarks>
/// The readout is built from the same pieces as the rest of the chart - a ribbon ring for the marker and
/// <see cref="EntityTextComponent"/> or <see cref="WorldTextComponent"/> for the label, following
/// <see cref="ChartOptions.LabelMode"/> - so it needs no UI page or font asset and looks right in both
/// presets. The mouse ray is intersected with the chart's plane, so it works with the orthographic 2D
/// camera and a free 3D camera alike, and respects the chart root's position, rotation and scale.
/// </remarks>
public sealed class ChartCursor : IDisposable
{
    private readonly Chart _chart;
    private readonly Entity _marker;
    private readonly ModelComponent _markerModel;
    private readonly Entity _labelEntity;
    private readonly EntityTextComponent? _screenText;
    private readonly WorldTextComponent? _worldText;
    private bool _isDisposed;

    /// <summary>
    /// The point under the mouse in chart units, or <see langword="null"/> while the cursor is off the chart -
    /// the value to read a curve against, or to snap something to.
    /// </summary>
    public Vector3? Position { get; private set; }

    internal ChartCursor(Game game, Chart chart)
    {
        _chart = chart;
        var o = chart.Options;

        _marker = game.CreatePolyline(
            PolylineSampling.Parametric(t => new Vector3(0.09f * MathF.Cos(t), 0.09f * MathF.Sin(t), 0f), 0f, MathUtil.TwoPi, 20),
            new PolylineOptions { Width = 0.03f, Color = o.LabelColor, Closed = true, EmissiveIntensity = o.CurveEmissiveIntensity },
            "Cursor marker");
        _markerModel = _marker.Get<ModelComponent>()!;
        _markerModel.Enabled = false;
        chart.Root.AddChild(_marker);

        _labelEntity = new Entity("Cursor readout");

        if (o.LabelMode == ChartLabelMode.Screen)
        {
            _screenText = new EntityTextComponent
            {
                Text = string.Empty,
                FontSize = o.LabelFontSize,
                TextColor = o.LabelColor,
                Anchor = TextAnchor.BottomLeft,
                Offset = new Vector2(10f, -10f),
                IsVisible = false,
            };
            _labelEntity.Add(_screenText);
        }
        else
        {
            _worldText = new WorldTextComponent
            {
                Text = string.Empty,
                Height = o.LabelHeight,
                TextColor = o.LabelColor,
                Anchor = TextAnchor.BottomLeft,
                Offset = new Vector3(0.15f, 0.15f, 0f),
                Billboard = true,
                KeepUpright = true,
                IsVisible = false,
            };
            _labelEntity.Add(_worldText);
        }

        chart.Root.AddChild(_labelEntity);
    }

    /// <summary>
    /// Moves the readout to the point of the chart plane under <paramref name="screenPosition"/>, or hides it
    /// when that point is outside the chart's ranges or behind the camera.
    /// </summary>
    /// <param name="camera">The camera the mouse position is relative to.</param>
    /// <param name="screenPosition">The mouse position in normalised screen coordinates - <c>Input.MousePosition</c> as it comes.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="camera"/> is <see langword="null"/>.</exception>
    public void Update(CameraComponent camera, Vector2 screenPosition)
    {
        ArgumentNullException.ThrowIfNull(camera);

        // The chart's plane in world space; the root may be moved, rotated and scaled
        var world = _chart.Root.Transform.WorldMatrix;
        var normal = Vector3.TransformNormal(Vector3.UnitZ, world);
        normal.Normalize();
        var plane = new Plane(world.TranslationVector, normal);

        var ray = camera.GetPickRay(screenPosition);

        if (!ray.Intersects(in plane, out Vector3 hit))
        {
            Hide();
            return;
        }

        Matrix.Invert(ref world, out var toChart);
        var local = Vector3.TransformCoordinate(hit, toChart);

        var o = _chart.Options;

        if (local.X < o.XMin || local.X > o.XMax || local.Y < o.YMin || local.Y > o.YMax)
        {
            Hide();
            return;
        }

        Position = local;

        // The ring is world geometry; scaling it with the view keeps it the same size on screen
        var scale = _chart.ViewScale;
        _marker.Transform.Scale = new Vector3(scale, scale, 1f);

        var position = new Vector3(local.X, local.Y, 3f * Chart.LayerStep);
        _marker.Transform.Position = position;
        _labelEntity.Transform.Position = position;
        _markerModel.Enabled = true;

        var text = $"x = {local.X.ToString(o.CursorFormat, CultureInfo.InvariantCulture)}  y = {local.Y.ToString(o.CursorFormat, CultureInfo.InvariantCulture)}";

        if (_screenText is not null)
        {
            _screenText.Text = text;
            _screenText.IsVisible = true;
        }

        if (_worldText is not null)
        {
            _worldText.Text = text;
            _worldText.IsVisible = true;
        }
    }

    /// <summary>
    /// Hides the marker and the label until the next <see cref="Update"/> that lands on the chart.
    /// </summary>
    public void Hide()
    {
        Position = null;
        _markerModel.Enabled = false;

        if (_screenText is not null)
            _screenText.IsVisible = false;

        if (_worldText is not null)
            _worldText.IsVisible = false;
    }

    /// <summary>
    /// Removes the marker and label from the chart and frees the ring's ribbon buffers. Called by
    /// <see cref="Chart.Dispose"/>; safe to call twice.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_markerModel.Model is { } model)
        {
            foreach (var mesh in model.Meshes)
            {
                PolylineMeshBuilder.Release(mesh);
            }
        }

        _chart.Root.RemoveChild(_marker);
        _chart.Root.RemoveChild(_labelEntity);
    }
}