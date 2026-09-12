using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.CommunityToolkit.Shapes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;

namespace E11_3D_ShapeBatch;

/// <summary>
/// One exhibit: what it is called, what it shows in a few words, the ShapeBatch member it is mostly
/// made of, how to draw it, and what it needs on the ground. The order of the registry is the
/// order of the gallery; inserting one shifts every number after it.
/// </summary>
/// <param name="Title">A short name, as the index board and the labels print it.</param>
/// <param name="Summary">A few words on what to look at, for the widest label.</param>
/// <param name="Method">The ShapeBatch member the station is mostly made of, via <c>nameof</c>.</param>
/// <param name="Draw">Draws the station every frame, in the station's own coordinates.</param>
/// <param name="Setup">Builds what the station needs once - text entities, props - or nothing.</param>
/// <param name="Pillars">How many of the standard pillars the station wants, 0 to 2.</param>
/// <param name="Anchor">Where the label's dotted line touches the exhibit, in station coordinates.</param>
public sealed record Demo(
    string Title,
    string Summary,
    string Method,
    Action<GalleryStation> Draw,
    Action<GalleryStation>? Setup = null,
    int Pillars = 0,
    Vector3? Anchor = null);

/// <summary>A solid block a station can hide shapes behind or hang markers from.</summary>
/// <param name="Base">The centre of its footprint on the ground, in world coordinates.</param>
/// <param name="Height">Its height; the top is <see cref="Base"/> lifted by this.</param>
public readonly record struct Pillar(Vector3 Base, float Height)
{
    /// <summary>The centre of the top face.</summary>
    public Vector3 Top => Base + Vector3.UnitY * Height;

    /// <summary>The centre of the block.</summary>
    public Vector3 Centre => Base + Vector3.UnitY * (Height * 0.5f);
}

/// <summary>The batches the gallery draws through, so a station can pick the one it is about.</summary>
/// <param name="Scene">Depth-tested: the ground and the pillars occlude these shapes.</param>
/// <param name="Overlay">Drawn over everything, the gizmo batch.</param>
/// <param name="Pictures">Depth-tested, filled with the gallery's picture, clamped at its edges.</param>
/// <param name="Stripes">Depth-tested, the same picture tiled four times across and free to scroll.</param>
/// <param name="Stripe">The node behind <see cref="Stripes"/>, whose offset a station animates.</param>
public sealed record GalleryBatches(ShapeBatch Scene, ShapeBatch Overlay, ShapeBatch Pictures, ShapeBatch Stripes, Stride.Rendering.Materials.ComputeColors.ComputeTextureColor Stripe);

/// <summary>The three per-frame states the visitor changes with keys, applied to every station.</summary>
public sealed class GalleryStyle
{
    public float BorderWidth { get; set; } = 3f;

    public float FillAlpha { get; set; } = 0.45f;

    public float GlowWidth { get; set; }
}

/// <summary>
/// One place in the gallery: a frame on the ground facing the centre, and everything a station's
/// draw method needs. A method draws in station coordinates - X to its right as the visitor sees
/// it, Y up, Z towards the visitor - and never learns where on the ring it stands, which is what
/// lets it be lifted into any game with a station at the origin.
/// </summary>
public sealed class GalleryStation
{
    public required int Number { get; init; }

    public required Demo Demo { get; init; }

    public required Game Game { get; init; }

    public required Scene Scene { get; init; }

    public required GalleryBatches Batches { get; init; }

    public required GalleryStyle Style { get; init; }

    /// <summary>The centre of the station's pad, on the ground.</summary>
    public required Vector3 Origin { get; init; }

    /// <summary>The station's X axis: to the right, as seen from the gallery's centre.</summary>
    public required Vector3 Right { get; init; }

    /// <summary>The station's Z axis: towards the gallery's centre, where the visitor stands.</summary>
    public required Vector3 Forward { get; init; }

    /// <summary>The station's Y axis, the world's up.</summary>
    public Vector3 Up { get; } = Vector3.UnitY;

    /// <summary>The batch to draw through this frame: the scene one, or the overlay when T says so.</summary>
    public ShapeBatch Shapes { get; set; } = null!;

    /// <summary>Elapsed time, for anything that moves.</summary>
    public float Seconds { get; set; }

    /// <summary>The standard pillars the station asked for, in the order they were placed.</summary>
    public List<Pillar> Pillars { get; } = [];

    /// <summary>Whatever <see cref="Demo.Setup"/> made for <see cref="Demo.Draw"/> to use.</summary>
    public object? State { get; set; }

    /// <summary>Whether the visitor is nearest this station this frame - what a station with screen-space content draws only for.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>A point in station coordinates, in the world.</summary>
    public Vector3 At(float x, float y, float z) => Origin + Right * x + Vector3.UnitY * y + Forward * z;

    /// <summary>A point in station coordinates, in the world.</summary>
    public Vector3 At(Vector3 local) => At(local.X, local.Y, local.Z);

    /// <summary>A direction in station coordinates, in the world; not normalised.</summary>
    public Vector3 Direction(float x, float y, float z) => Right * x + Vector3.UnitY * y + Forward * z;

    /// <summary>
    /// The rotation of an entity whose X and Y lie in the station's upright plane and whose Z faces
    /// the visitor - what a world-text component with billboarding off needs to read from the front.
    /// </summary>
    public Quaternion FacingRotation()
    {
        var basis = Matrix.Identity;
        basis.Right = Right;
        basis.Up = Vector3.UnitY;
        basis.Backward = Forward;

        return Quaternion.RotationMatrix(basis);
    }

    /// <summary>
    /// Puts the gallery's current style back on a batch: the visitor's border, fill and glow, and
    /// none of the per-draw states a previous station may have left on.
    /// </summary>
    public void ResetStyle(ShapeBatch batch)
    {
        batch.BorderWidth = Style.BorderWidth;
        batch.Fill.Set(null, Style.FillAlpha);
        batch.Glow.Clear();
        batch.Glow.Width = Style.GlowWidth;
        batch.Dash.Clear();
        batch.Gradient.Clear();
        batch.Opacity = 1f;
        batch.DepthFade = 0f;

        // Per-draw like the rest: a station that turned it off for a bracket would otherwise leave
        // every later panel in the batch untextured
        batch.Textured = true;
        batch.Screen = false;
        batch.Viewport = null;
    }
}

/// <summary>
/// The ring of stations. Given the ordered registry it places every station on a circle facing the
/// centre, builds the ground, the pillars each station asked for, the numbered labels with their
/// dotted lines and the index board, and draws all of it every frame. Nothing here is positioned by
/// hand: add a demo to the registry and the ring grows to fit.
/// </summary>
public sealed class Gallery
{
    /// <summary>Arc length per station, in world units: a pad, and a gap to the next one.</summary>
    private const float Spacing = 13f;

    /// <summary>The ring is never tighter than this, so a short registry still has room to fly.</summary>
    private const float MinimumRadius = 22f;

    /// <summary>How high above the pad the label pin floats.</summary>
    private const float PinHeight = 7.5f;

    private readonly Game _game;
    private readonly Scene _scene;
    private readonly List<GalleryStation> _stations = [];
    private readonly List<EntityTextComponent> _labels = [];
    private readonly Material _pillarMaterial;
    private WorldTextComponent? _board;

    private readonly GalleryCamera _camera;
    private int _destination;

    public Gallery(Game game, Scene scene, IReadOnlyList<Demo> demos, GalleryBatches batches, GalleryStyle style)
    {
        _game = game;
        _scene = scene;
        Batches = batches;
        Style = style;
        Radius = MathF.Max(MinimumRadius, demos.Count * Spacing / MathF.Tau);
        _camera = new GalleryCamera(game);

        BuildGround();
        _pillarMaterial = game.CreateMaterial(new Color(96, 103, 116), specular: 0.1f, microSurface: 0.35f);

        for (var i = 0; i < demos.Count; i++)
        {
            _stations.Add(BuildStation(i, demos.Count, demos[i]));
        }

        BuildBoard();
    }

    public GalleryBatches Batches { get; }

    public GalleryStyle Style { get; }

    public float Radius { get; }

    public IReadOnlyList<GalleryStation> Stations => _stations;

    /// <summary>What the labels say: the number, the number and the method, or everything.</summary>
    public int LabelDetail { get; set; }

    /// <summary>When set, only the station nearest the camera draws - one exhibit at a time.</summary>
    public bool Solo { get; set; }

    /// <summary>Whether stations draw through the depth-tested batch or the overlay.</summary>
    public bool DepthTested { get; set; } = true;

    /// <summary>The station nearest the camera, updated every frame; N and P move from it.</summary>
    public int Current { get; private set; }

    /// <summary>How many shapes the last frame submitted, over every batch.</summary>
    public int Submitted { get; private set; }

    /// <summary>Whether the camera is flying itself somewhere rather than being steered.</summary>
    public bool Flying => _camera.Flying;

    /// <summary>
    /// Where the next and previous keys count from: the station being flown to while a flight is
    /// running, so pressing next twice quickly skips two stations rather than re-aiming at the one
    /// the camera happens to be passing.
    /// </summary>
    public int Focus => Flying ? _destination : Current;

    /// <summary>Sends the visitor home: in front of the index board, the ring all around.</summary>
    /// <param name="instant">Put the camera there at once, rather than flying it.</param>
    public void GoHome(bool instant = false)
    {
        _destination = Current;

        _camera.FlyTo(new Vector3(0f, 6.2f, 22f), Quaternion.RotationYawPitchRoll(0f, MathUtil.DegreesToRadians(-4f), 0f), instant);
    }

    /// <summary>Sends the visitor to a station: a little way towards the centre from its pad, looking at it.</summary>
    /// <param name="index">The station, counted from 0 and wrapped, so one past the last is the first.</param>
    /// <param name="instant">Put the camera there at once, rather than flying it.</param>
    public void GoTo(int index, bool instant = false)
    {
        _destination = (index % _stations.Count + _stations.Count) % _stations.Count;

        var station = _stations[_destination];
        var eye = station.At(0f, 4.5f, 13f);
        var target = station.At(0f, 1.6f, -1f);
        var direction = Vector3.Normalize(target - eye);

        // A camera looks down its -Z; yaw turns that towards -X, pitch lifts it
        var yaw = MathF.Atan2(-direction.X, -direction.Z);
        var pitch = MathF.Asin(direction.Y);

        _camera.FlyTo(eye, Quaternion.RotationYawPitchRoll(yaw, pitch, 0f), instant);
    }

    /// <summary>
    /// Advances whatever is in flight and submits every station's shapes. Immediate mode: this is
    /// the frame, called from the game's update.
    /// </summary>
    public void Update(GameTime time)
    {
        _camera.Update((float)time.Elapsed.TotalSeconds);
        Draw((float)time.Total.TotalSeconds);
    }

    private void Draw(float seconds)
    {
        var camera = _game.GetCameraEntity().Transform.Position;

        Current = NearestStation(camera);

        var shapes = DepthTested ? Batches.Scene : Batches.Overlay;
        var before = Batches.Scene.Count + Batches.Overlay.Count + Batches.Pictures.Count + Batches.Stripes.Count;

        foreach (var station in _stations)
        {
            var visible = !Solo || station.Number - 1 == Current;

            station.Shapes = shapes;
            station.Seconds = seconds;
            station.IsCurrent = station.Number - 1 == Current;
            station.ResetStyle(Batches.Scene);
            station.ResetStyle(Batches.Overlay);
            station.ResetStyle(Batches.Pictures);
            station.ResetStyle(Batches.Stripes);

            DrawPad(station);

            if (visible)
            {
                station.Demo.Draw(station);
            }
        }

        Submitted = Batches.Scene.Count + Batches.Overlay.Count + Batches.Pictures.Count + Batches.Stripes.Count - before;

        DrawLabels();
        DrawBoardFrame();
    }

    /// <summary>The label texts follow the detail level; called when L changes it.</summary>
    public void UpdateLabels()
    {
        for (var i = 0; i < _stations.Count; i++)
        {
            var demo = _stations[i].Demo;

            _labels[i].Text = LabelDetail switch
            {
                0 => $"{i + 1}",
                1 => $"{i + 1}  {demo.Method}",
                _ => $"{i + 1}  {demo.Method}\n{demo.Title}: {demo.Summary}",
            };
        }
    }

    private GalleryStation BuildStation(int index, int count, Demo demo)
    {
        // Station 1 straight ahead of the starting camera, the rest clockwise as seen from above
        var angle = -MathF.PI * 0.5f + index * MathF.Tau / count;
        var (sin, cos) = MathF.SinCos(angle);
        var outward = new Vector3(cos, 0f, sin);
        var forward = -outward;
        var right = Vector3.Cross(Vector3.UnitY, forward);

        var station = new GalleryStation
        {
            Number = index + 1,
            Demo = demo,
            Game = _game,
            Scene = _scene,
            Batches = Batches,
            Style = Style,
            Origin = outward * Radius,
            Right = right,
            Forward = forward,
        };

        // The standard pillars: one left and back, a taller one right and further back
        ReadOnlySpan<(Vector3 Local, float Height)> spots = [(new Vector3(-3.5f, 0f, -1.5f), 4.5f), (new Vector3(3.5f, 0f, -3.5f), 6f)];

        for (var i = 0; i < Math.Min(demo.Pillars, spots.Length); i++)
        {
            var (local, height) = spots[i];
            var basePoint = station.At(local);

            var pillar = _game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions
            {
                EntityName = $"Station {station.Number} pillar {i + 1}",
                Material = _pillarMaterial,
                Size = new Vector3(1.8f, height, 1.8f),
                Position = basePoint + Vector3.UnitY * (height * 0.5f),
            });

            pillar.Scene = _scene;
            station.Pillars.Add(new Pillar(basePoint, height));
        }

        demo.Setup?.Invoke(station);

        // The label: screen-space text pinned to a point above the pad, so it reads at any distance
        var label = new EntityTextComponent
        {
            Text = $"{station.Number}",
            FontSize = 20,
            TextColor = Color.White,
            Anchor = Stride.CommunityToolkit.Rendering.Text.TextAnchor.MiddleLeft,
            Offset = new Vector2(10f, 0f),
            EnableBackground = true,
            BackgroundColor = new Color4(0.03f, 0.05f, 0.09f, 0.85f),
            EnableShadow = false,
        };

        var entity = new Entity($"Station {station.Number} label") { Transform = { Position = PinOf(station) } };

        entity.Add(label);
        entity.Scene = _scene;
        _labels.Add(label);

        return station;
    }

    private void BuildGround()
    {
        // Dark and matte, so the shapes read against it instead of fighting a specular hotspot
        var groundMaterial = _game.CreateMaterial(new Color(38, 41, 47), specular: 0.04f, microSurface: 0.25f);

        // Room for the ring, and for the stations that run outwards - the corridor of rings, the rails
        var side = (Radius + 80f) * 2f;

        var ground = _game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions
        {
            EntityName = "Ground",
            Material = groundMaterial,
            Size = new Vector3(side, 0.5f, side),
            Position = new Vector3(0f, -0.25f, 0f),
        });

        ground.Scene = _scene;
    }

    /// <summary>
    /// The index board at the centre: a HUD panel listing every station, drawn each frame like any
    /// other shape, with the list as world text placed once.
    /// </summary>
    private void BuildBoard()
    {
        var lines = string.Join('\n', _stations.Select(s => $"{s.Number,2}  {s.Demo.Title}"));

        _board = new WorldTextComponent
        {
            Text = lines,
            FontSize = 40,
            // The height is the whole block's, every line of it; hung from the top of the frame
            Height = _stations.Count * LineHeight,
            TextColor = new Color(130, 205, 255),
            GlowColor = new Color(0, 140, 255, 120),
            GlowSize = 3f,
            Anchor = Stride.CommunityToolkit.Rendering.Text.TextAnchor.TopCenter,
            Alignment = TextAlignment.Left,
            Billboard = false,
        };

        var entity = new Entity("Index board") { Transform = { Position = BoardCentre + Vector3.UnitY * (BoardSize.Y * 0.5f - 0.5f) + Vector3.UnitZ * 0.01f } };

        entity.Add(_board);
        entity.Scene = _scene;
    }

    private static Vector3 BoardCentre => new(0f, 5.6f, 0f);

    /// <summary>One line of the index board, in world units; the frame is the lines plus a margin.</summary>
    private const float LineHeight = 0.42f;

    private Vector2 BoardSize => new(11f, MathF.Max(4f, _stations.Count * LineHeight + 1f));

    private void DrawBoardFrame()
    {
        var shapes = Batches.Scene;
        var hudBlue = new Color(110, 200, 255);

        shapes.Fill.Set(new Color(4, 14, 30), 0.8f);
        shapes.BorderWidth = 1.5f;
        shapes.Glow.Set(7f, new Color(0, 150, 255, 160));
        shapes.DrawRectangle(BoardCentre, Vector3.UnitX, Vector3.UnitY, BoardSize, hudBlue, cornerRadius: 0.35f);
        shapes.Glow.Clear();

        // A title above the board, and its corner brackets, the HUD cliche
        var half = BoardSize * 0.5f;
        var topLeft = BoardCentre + new Vector3(-half.X + 0.3f, half.Y - 0.3f, 0f);
        var bottomRight = BoardCentre + new Vector3(half.X - 0.3f, -half.Y + 0.3f, 0f);

        shapes.DrawPixelLine(topLeft, topLeft + Vector3.UnitX * 0.8f, 1.5f, hudBlue);
        shapes.DrawPixelLine(topLeft, topLeft - Vector3.UnitY * 0.5f, 1.5f, hudBlue);
        shapes.DrawPixelLine(bottomRight, bottomRight - Vector3.UnitX * 0.8f, 1.5f, hudBlue);
        shapes.DrawPixelLine(bottomRight, bottomRight + Vector3.UnitY * 0.5f, 1.5f, hudBlue);
    }

    /// <summary>A faint ring under every station, so a pad reads as a place even when its exhibit is small.</summary>
    private void DrawPad(GalleryStation station)
    {
        var shapes = Batches.Scene;
        var current = station.Number - 1 == Current;

        shapes.BorderWidth = current ? 2f : 1f;
        shapes.Opacity = current ? 0.9f : 0.35f;
        shapes.DrawRing(station.Origin + Vector3.UnitY * 0.01f, Vector3.UnitY, 5.8f, current ? new Color(150, 210, 255) : new Color(110, 140, 170));
        station.ResetStyle(shapes);
    }

    /// <summary>
    /// The dotted line from each exhibit up to its pin, through the overlay batch so a pillar never
    /// hides it: a small ring where it touches the shape, a dot where the label hangs.
    /// </summary>
    private void DrawLabels()
    {
        var shapes = Batches.Overlay;

        shapes.BorderWidth = 1.5f;
        shapes.Dash.Set(2f, 5f);

        foreach (var station in _stations)
        {
            var visible = !Solo || station.Number - 1 == Current;

            _labels[station.Number - 1].IsVisible = visible;

            if (!visible) continue;

            var anchor = station.At(station.Demo.Anchor ?? new Vector3(0f, 0.3f, 0f));
            var pin = PinOf(station);

            shapes.DrawPixelLine(anchor, pin, 1.5f, Color.White);
        }

        shapes.Dash.Clear();

        foreach (var station in _stations)
        {
            if (Solo && station.Number - 1 != Current) continue;

            var anchor = station.At(station.Demo.Anchor ?? new Vector3(0f, 0.3f, 0f));

            shapes.DrawPixelRing(anchor, 4f, Color.White);
            shapes.DrawPixelDisc(PinOf(station), 4f, Color.White);
        }

        _stations[0].ResetStyle(shapes);
    }

    private static Vector3 PinOf(GalleryStation station)
    {
        var anchor = station.Demo.Anchor ?? new Vector3(0f, 0.3f, 0f);

        return station.At(anchor.X + 1.2f, PinHeight, anchor.Z);
    }

    private int NearestStation(Vector3 position)
    {
        var nearest = 0;
        var best = float.MaxValue;

        for (var i = 0; i < _stations.Count; i++)
        {
            var distance = Vector3.DistanceSquared(position, _stations[i].Origin);

            if (distance < best)
            {
                best = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    /// <summary>
    /// A 128 by 128 picture with an unmistakable orientation: a warm-to-cool diagonal, a grid of
    /// dark lines, a white square in the top-left corner and a black one at the bottom right.
    /// </summary>
    public static Texture CreatePicture(GraphicsDevice device)
    {
        const int size = 128;
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var t = 0.5f * (x + y) / (size - 1);
                var color = Color.Lerp(new Color(255, 120, 40), new Color(40, 110, 255), t);

                if (x % 16 == 0 || y % 16 == 0)
                {
                    color = new Color(20, 20, 30);
                }

                if (x < 24 && y < 24)
                {
                    color = Color.White;
                }
                else if (x >= size - 24 && y >= size - 24)
                {
                    color = Color.Black;
                }

                pixels[y * size + x] = color;
            }
        }

        return Texture.New2D(device, size, size, PixelFormat.R8G8B8A8_UNorm_SRgb, pixels);
    }
}