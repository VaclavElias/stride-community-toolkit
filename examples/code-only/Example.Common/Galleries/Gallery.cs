using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.CommunityToolkit.Shapes;
using Stride.Graphics;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;

namespace Example.Common.Galleries;

/// <summary>
/// A ring of stations. Given an ordered registry of exhibits it places every station on a circle
/// facing the centre, builds the ground, the pillars each exhibit asked for, the numbered labels
/// with their dotted lines and the index board, flies the camera between them, and runs every
/// exhibit's update each frame. Nothing here is positioned by hand: add an exhibit to the registry
/// and the ring grows to fit.
/// </summary>
/// <typeparam name="TStation">The kind of station handed to the exhibits: <see cref="GalleryStation"/>, or an example's own with more on it.</typeparam>
/// <remarks>
/// The gallery draws its own furniture - pads, dotted lines, the board's frame - through two shape
/// batches of its own, so an example's batches and their per-draw state are never touched by it.
/// What an exhibit draws or animates is the exhibit's business, through <see cref="Prepare"/> and
/// the exhibit's update.
/// </remarks>
public sealed class Gallery<TStation> where TStation : GalleryStation, new()
{
    /// <summary>How high above the pad the label pin floats.</summary>
    private const float PinHeight = 7.5f;

    /// <summary>One line of the index board, in world units; the frame is the lines plus a margin.</summary>
    private const float LineHeight = 0.45f;

    private static readonly Vector3 BoardCentre = new(0f, 5.6f, 0f);

    private readonly Game _game;
    private readonly Scene _scene;
    private readonly IReadOnlyList<Exhibit<TStation>> _exhibits;
    private readonly List<TStation> _stations = [];
    private readonly List<EntityTextComponent> _labels = [];
    private readonly Material _pillarMaterial;
    private readonly ShapeBatch _furniture;
    private readonly ShapeBatch _overlay;
    private readonly GalleryCamera _camera;
    private int _destination;

    /// <summary>
    /// Builds the ring: the ground, every station with its pillars and label, the index board,
    /// and runs each exhibit's setup. The camera is left where it is; call <see cref="GoHome"/> or
    /// <see cref="GoTo"/> to place it.
    /// </summary>
    /// <param name="game">The game, with its compositor and camera already set up.</param>
    /// <param name="scene">The scene the furniture and the exhibits' entities go into.</param>
    /// <param name="exhibits">The registry, in gallery order.</param>
    /// <param name="configure">Runs on each station after its frame is set and before its exhibit's setup: where an example puts its own things on the station.</param>
    /// <param name="options">Layout figures; the shape gallery's by default.</param>
    public Gallery(Game game, Scene scene, IReadOnlyList<Exhibit<TStation>> exhibits, Action<TStation>? configure = null, GalleryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(exhibits);

        _game = game;
        _scene = scene;
        _exhibits = exhibits;
        Options = options ?? GalleryOptions.Default;
        Radius = MathF.Max(Options.MinimumRadius, exhibits.Count * Options.Spacing / MathF.Tau);
        _camera = new GalleryCamera(game);
        _pillarMaterial = game.CreateMaterial(Options.PillarColor ?? new Color(96, 103, 116), specular: 0.1f, microSurface: 0.35f);

        // Depth-tested for the pads and the board, over everything for the dotted lines and pins
        _furniture = game.AddShapeBatch(depthTest: true);
        _overlay = game.AddShapeBatch(depthTest: false);

        BuildGround();

        for (var i = 0; i < exhibits.Count; i++)
        {
            _stations.Add(BuildStation(i, exhibits.Count, exhibits[i], configure));
        }

        BuildBoard();
    }

    /// <summary>The layout figures the ring was built with.</summary>
    public GalleryOptions Options { get; }

    /// <summary>The ring's radius, from the registry's length.</summary>
    public float Radius { get; }

    /// <summary>The stations, in registry order.</summary>
    public IReadOnlyList<TStation> Stations => _stations;

    /// <summary>What the labels say: the number, the number and the method, or everything.</summary>
    public int LabelDetail { get; set; }

    /// <summary>When set, only the station nearest the camera runs and shows its label - one exhibit at a time.</summary>
    public bool Solo { get; set; }

    /// <summary>The station nearest the camera, updated every frame; next and previous count from it.</summary>
    public int Current { get; private set; }

    /// <summary>Whether the camera is flying itself somewhere rather than being steered.</summary>
    public bool Flying => _camera.Flying;

    /// <summary>
    /// Where the next and previous keys count from: the station being flown to while a flight is
    /// running, so pressing next twice quickly skips two stations rather than re-aiming at the one
    /// the camera happens to be passing.
    /// </summary>
    public int Focus => Flying ? _destination : Current;

    /// <summary>
    /// Runs on every station before its exhibit's update each frame: where an example puts the
    /// frame's state on the station - which batch to draw through, the visitor's style - so that
    /// every exhibit starts from the same place.
    /// </summary>
    public Action<TStation>? Prepare { get; set; }

    /// <summary>Sends the visitor home: in front of the index board, the ring all around.</summary>
    /// <param name="instant">Put the camera there at once, rather than flying it.</param>
    public void GoHome(bool instant = false)
    {
        _destination = Current;
        // Further back and higher for a long registry, so the whole board fits the view
        var extra = MathF.Max(0f, BoardSize.Y - 12f);

        _camera.FlyTo(new Vector3(0f, 6.2f + extra * 0.4f, 22f + extra * 1.6f), Quaternion.RotationYawPitchRoll(0f, MathUtil.DegreesToRadians(-4f), 0f), instant);
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
    /// Advances whatever is in flight, finds the station the visitor is nearest, runs every shown
    /// exhibit's update and draws the furniture. Immediate mode: this is the frame, called from
    /// the game's update.
    /// </summary>
    /// <param name="time">The frame's time.</param>
    public void Update(GameTime time)
    {
        _camera.Update((float)time.Elapsed.TotalSeconds);

        var seconds = (float)time.Total.TotalSeconds;
        var camera = _game.GetCameraEntity().Transform.Position;

        Current = NearestStation(camera);

        for (var i = 0; i < _stations.Count; i++)
        {
            var station = _stations[i];
            var current = i == Current;

            station.Seconds = seconds;
            station.IsCurrent = current;

            DrawPad(station, current);

            if (Solo && !current) continue;

            Prepare?.Invoke(station);
            _exhibits[i].Update?.Invoke(station);
        }

        DrawLabels();
        DrawBoardFrame();
    }

    /// <summary>The label texts follow the detail level; call it after changing <see cref="LabelDetail"/>.</summary>
    public void UpdateLabels()
    {
        for (var i = 0; i < _stations.Count; i++)
        {
            var exhibit = _stations[i].Exhibit;

            _labels[i].Text = LabelDetail switch
            {
                0 => $"{i + 1}",
                1 => $"{i + 1}  {exhibit.Method}",
                _ => $"{i + 1}  {exhibit.Method}\n{exhibit.Title}: {exhibit.Summary}",
            };
        }
    }

    private TStation BuildStation(int index, int count, Exhibit<TStation> exhibit, Action<TStation>? configure)
    {
        // Station 1 straight ahead of the starting camera, the rest clockwise as seen from above
        var angle = -MathF.PI * 0.5f + index * MathF.Tau / count;
        var (sin, cos) = MathF.SinCos(angle);
        var outward = new Vector3(cos, 0f, sin);
        var forward = -outward;
        var right = Vector3.Cross(Vector3.UnitY, forward);

        var station = new TStation
        {
            Number = index + 1,
            Exhibit = exhibit,
            Game = _game,
            Scene = _scene,
            Origin = outward * Radius,
            Right = right,
            Forward = forward,
        };

        // The standard pillars: one left and back, a taller one right and further back
        ReadOnlySpan<(Vector3 Local, float Height)> spots = [(new Vector3(-3.5f, 0f, -1.5f), 4.5f), (new Vector3(3.5f, 0f, -3.5f), 6f)];

        for (var i = 0; i < Math.Min(exhibit.Pillars, spots.Length); i++)
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

        configure?.Invoke(station);
        exhibit.Setup?.Invoke(station);

        // The label: screen-space text pinned to a point above the pad, so it reads at any distance
        var label = new EntityTextComponent
        {
            Text = $"{station.Number}",
            FontSize = 20,
            TextColor = Color.White,
            Anchor = TextAnchor.MiddleLeft,
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
        // Dark and matte, so exhibits read against it instead of fighting a specular hotspot
        var groundMaterial = _game.CreateMaterial(new Color(38, 41, 47), specular: 0.04f, microSurface: 0.25f);

        // Room for the ring, and for the exhibits that run outwards
        var side = (Radius + Options.GroundMargin) * 2f;

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
        var lines = string.Join('\n', _stations.Select(s => $"{s.Number,2}  {s.Exhibit.Title}"));

        var board = new WorldTextComponent
        {
            Text = lines,
            FontSize = 40,
            // The height is the whole block's, every line of it; hung from the top of the frame
            Height = _stations.Count * LineHeight,
            TextColor = new Color(130, 205, 255),
            GlowColor = new Color(0, 140, 255, 120),
            GlowSize = 3f,
            Anchor = TextAnchor.TopCenter,
            Alignment = TextAlignment.Left,
            Billboard = false,
        };

        var entity = new Entity("Index board") { Transform = { Position = BoardCentre + Vector3.UnitY * (BoardSize.Y * 0.5f - 0.5f) + Vector3.UnitZ * 0.01f } };

        entity.Add(board);
        entity.Scene = _scene;
    }

    private Vector2 BoardSize => new(11f, MathF.Max(4f, _stations.Count * LineHeight + 1f));

    private void DrawBoardFrame()
    {
        var shapes = _furniture;
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

        shapes.Fill.Set(null, 0.45f);
        shapes.BorderWidth = 3f;
    }

    /// <summary>A faint ring under every station, so a pad reads as a place even when its exhibit is small.</summary>
    private void DrawPad(GalleryStation station, bool current)
    {
        var shapes = _furniture;

        shapes.BorderWidth = current ? 2f : 1f;
        shapes.Opacity = current ? 0.9f : 0.35f;
        shapes.DrawRing(station.Origin + Vector3.UnitY * 0.01f, Vector3.UnitY, 5.8f, current ? new Color(150, 210, 255) : new Color(110, 140, 170));
        shapes.Opacity = 1f;
        shapes.BorderWidth = 3f;
    }

    /// <summary>
    /// The dotted line from each exhibit up to its pin, through the overlay batch so a pillar never
    /// hides it: a small ring where it touches the exhibit, a dot where the label hangs.
    /// </summary>
    private void DrawLabels()
    {
        var shapes = _overlay;

        shapes.BorderWidth = 1.5f;
        shapes.Dash.Set(2f, 5f);

        foreach (var station in _stations)
        {
            var visible = !Solo || station.Number - 1 == Current;

            _labels[station.Number - 1].IsVisible = visible;

            if (!visible) continue;

            shapes.DrawPixelLine(AnchorOf(station), PinOf(station), 1.5f, Color.White);
        }

        shapes.Dash.Clear();

        foreach (var station in _stations)
        {
            if (Solo && station.Number - 1 != Current) continue;

            shapes.DrawPixelRing(AnchorOf(station), 4f, Color.White);
            shapes.DrawPixelDisc(PinOf(station), 4f, Color.White);
        }

        shapes.BorderWidth = 3f;
    }

    private static Vector3 AnchorOf(GalleryStation station) => station.At(station.Exhibit.Anchor ?? new Vector3(0f, 0.3f, 0f));

    private static Vector3 PinOf(GalleryStation station)
    {
        var anchor = station.Exhibit.Anchor ?? new Vector3(0f, 0.3f, 0f);

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
}