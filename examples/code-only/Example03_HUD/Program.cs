using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Shapes;
using Stride.CommunityToolkit.Windows;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Graphics.Font;
using Stride.Input;

// A ship's cockpit HUD, composed from the pieces the panels gallery shows one at a time: framed
// panels, bars, ring gauges, a radar, a heading tape, a pitch ladder, a reticle, status tiles, and
// the states a real HUD needs - selected against idle, disabled, warning against nominal.
//
// Everything that is a shape goes through one ShapeBatch and out in one draw call; everything that
// is text is a WorldTextComponent, created once and updated in place. The data is a simulated ship
// that flies itself - speed, altitude, heading and pitch drift, the shield takes hits and recharges,
// contacts orbit on the radar - so every widget is moving and every state shows up within a minute.
//
// T opens the colour schemes, 1-5 pick one, TAB moves the selection along the status tiles, SPACE
// freezes the simulation. Wheel and right-drag zoom into any widget.

const float ViewHeight = 19f;
const float ThinLine = 1.5f;
const float ThickLine = 2.5f;

ShapeBatch? shapes = null;
SpriteFont? sansFont = null;
SpriteFont? boldFont = null;
SpriteFont? monoFont = null;

var themeIndex = 0;
var paused = false;
var time = 0f;
var selectedTile = 1;
var lastShapeCount = 0;
var ship = new ShipState();

Dictionary<string, Label> labels = [];
List<(WorldTextComponent Text, Action<WorldTextComponent, Theme> Restyle)> themedText = [];
DebugTextDropdown? themeMenu = null;

// Dark ground, one bright accent, a text colour lighter than the accent, a glow deeper than it -
// plus the two colours a HUD needs that a theme must not change: amber and red
Theme[] themes =
[
    new("Blue", new Color(90, 190, 255), new Color(8, 22, 42), new Color(205, 238, 255), new Color(0, 140, 255)),
    new("Red", new Color(255, 95, 90), new Color(36, 10, 12), new Color(255, 210, 205), new Color(255, 40, 40)),
    new("Green", new Color(80, 245, 150), new Color(6, 30, 20), new Color(200, 255, 225), new Color(0, 220, 120)),
    new("Purple", new Color(185, 130, 255), new Color(24, 12, 44), new Color(230, 210, 255), new Color(150, 60, 255)),
    new("Orange", new Color(255, 170, 70), new Color(40, 20, 6), new Color(255, 228, 190), new Color(255, 130, 0)),
];

string[] commsLog =
[
    "Steel-Legacy > wait first fight me lol",
    "MisterBaxter > but I dont wanna wait 10 minutes",
    "Steel-Legacy > im here",
    "outside grim",
    "MisterBaxter > ok",
    "gimme a minute",
    "Steel-Legacy > copy",
    "switching ships?",
    "MisterBaxter > hmm",
    "we having a real fight",
    "85x is a little transport snub",
    "Steel-Legacy > look",
];

string[] tileNames = ["BADGER", "BADGER", "BADGER", "BADGER"];
string[] modeNames = ["VTOL", "GEAR", "LOCK", "DECOY"];

// Before the window exists: a sharp window on a scaled display, and the overlay then sizes itself
WindowsDpiManager.EnablePerMonitorV2();

using var game = new Game();

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.Window.AllowUserResizing = true;
    game.Window.Title = "Ship HUD";

    game.SetupBase2D(new Color(5, 7, 12));
    game.Add2DCameraController();
    game.AddWorldTextRenderer();

    shapes = game.AddShapeBatch();

    var camera = game.GetCameraEntity().Get<CameraComponent>();

    camera.OrthographicSize = ViewHeight;

    sansFont = SystemFonts.LoadFirst(game.Services, SystemFonts.SansSerifCandidates, 48);
    boldFont = SystemFonts.LoadFirst(game.Services, SystemFonts.SansSerifCandidates, 48, FontStyle.Bold);
    monoFont = SystemFonts.LoadFirst(game.Services, SystemFonts.MonospaceCandidates, 48);

    CreateLabels(scene);

    themeMenu = new DebugTextDropdown
    {
        Title = "Scheme",
        ToggleKey = Keys.T,
        TitleColor = Color.Yellow,
        SelectedIndex = themeIndex,
        Items = [.. themes.Index().Select(pair => new DebugTextDropdownItem(
            (Keys)(Keys.D1 + pair.Index), pair.Item.Name, () => ApplyTheme(pair.Index)))],
    };

    var overlay = DebugOverlay.GetOrCreate(game);

    // The top-left of the HUD is left clear for this
    overlay.Position = DisplayPosition.TopLeft;
    overlay.AddSection("HUD", OverlayLines);
}

void Update(Scene scene, GameTime gameTime)
{
    if (shapes is null) return;

    themeMenu?.Update(game.Input);

    if (game.Input.IsKeyPressed(Keys.Space)) paused = !paused;
    if (game.Input.IsKeyPressed(Keys.Tab)) selectedTile = (selectedTile + 1) % tileNames.Length;

    if (!paused)
    {
        time += (float)gameTime.Elapsed.TotalSeconds;
        ship.Advance(time);
    }

    var theme = themes[themeIndex];

    DrawFrame(theme);
    DrawHeadingTape(new Vector2(0f, 7.4f), 14f, theme);
    DrawReticle(new Vector2(0f, 1.6f), theme);
    DrawPitchLadder(new Vector2(0f, 1.6f), theme);
    DrawWarningStrip(new Vector2(0f, -2.3f), theme);
    DrawTargetBox(theme);

    DrawVerticalGauge(new Vector2(-9.4f, 2.2f), 5.0f, ship.Speed / 300f, "speed", theme);
    DrawVerticalGauge(new Vector2(9.4f, 2.2f), 5.0f, ship.Altitude / 2000f, "altitude", theme);

    DrawRadar(new Vector2(-12.6f, 0.4f), 2.7f, theme);
    DrawCommsPanel(new Vector2(-12.4f, -5.6f), new Vector2(7.2f, 5.2f), theme);

    DrawRingGauge(new Vector2(-4.6f, -4.6f), 1.25f, ship.Power, "power", theme.Accent, theme);
    DrawRingGauge(new Vector2(0f, -4.6f), 1.25f, ship.Temperature, "temperature", ship.Temperature > 0.8f ? Theme.Warning : theme.Accent, theme);
    DrawRingGauge(new Vector2(4.6f, -4.6f), 1.25f, ship.Thrust, "thrust", theme.Accent, theme);

    DrawSparkline(new Vector2(-6.8f, -1.6f), new Vector2(4.2f, 1.5f), theme);
    DrawBarChart(new Vector2(6.8f, -1.6f), new Vector2(4.2f, 1.5f), theme);

    DrawSystemsPanel(new Vector2(0f, -8.0f), new Vector2(9.2f, 2.6f), theme);

    for (var i = 0; i < tileNames.Length; i++)
    {
        DrawStatusTile(new Vector2(12.6f, 3.6f - i * 1.35f), new Vector2(5.0f, 1.1f), i, theme);
    }

    for (var i = 0; i < modeNames.Length; i++)
    {
        DrawModeButton(new Vector2(8.4f + i * 2.25f, -7.6f), new Vector2(2.0f, 1.0f), i, theme);
    }

    // Read before the frame renders - the batch empties itself once it has drawn
    lastShapeCount = shapes.Count;
}

// --- Widgets ---------------------------------------------------------------------------------

/// <summary>Four L-brackets in the corners: the edge of the canopy glass, the frame of the whole thing.</summary>
void DrawFrame(Theme theme)
{
    const float Arm = 1.6f;

    Style(ThinLine, 0f);

    foreach (var (sx, sy) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
    {
        var corner = new Vector3(sx * 16.2f, sy * 9.0f, 0f);

        shapes!.DrawPixelLine(corner, corner - new Vector3(sx * Arm, 0f, 0f), ThickLine, theme.Accent);
        shapes.DrawPixelLine(corner, corner - new Vector3(0f, sy * Arm, 0f), ThickLine, theme.Accent);
    }
}

/// <summary>
/// The compass strip: a window onto a 360-degree ruler that scrolls as the heading changes, with
/// the current heading boxed under a centre marker. The tick labels are ten text components reused
/// for whichever multiples of ten are in the window this frame.
/// </summary>
void DrawHeadingTape(Vector2 center, float width, Theme theme)
{
    const float DegreesShown = 80f;
    const float LabelHeight = 0.26f;

    var unitsPerDegree = width / DegreesShown;
    var half = width / 2f;
    var top = center.Y + 0.35f;

    Style(ThinLine, 0f);

    // The rule itself, then the ticks along it
    shapes!.DrawPixelLine(new Vector3(center.X - half, center.Y, 0f), new Vector3(center.X + half, center.Y, 0f), ThinLine, theme.Accent);

    var labelIndex = 0;
    var firstTick = MathF.Floor((ship.Heading - DegreesShown / 2f) / 5f) * 5f;

    for (var degrees = firstTick; degrees <= ship.Heading + DegreesShown / 2f; degrees += 5f)
    {
        var x = center.X + (degrees - ship.Heading) * unitsPerDegree;

        if (MathF.Abs(x - center.X) > half) continue;

        var major = MathF.Abs(degrees % 10f) < 0.01f;
        var tick = major ? 0.32f : 0.16f;

        shapes.DrawPixelLine(new Vector3(x, center.Y, 0f), new Vector3(x, center.Y + tick, 0f), ThinLine, theme.Accent);

        if (major && labelIndex < 10)
        {
            var normalised = ((degrees % 360f) + 360f) % 360f;

            SetLabel($"heading-tick-{labelIndex}", $"{normalised:000}", new Vector2(x, top + 0.32f), visible: true);

            labelIndex++;
        }
    }

    for (; labelIndex < 10; labelIndex++)
    {
        SetLabel($"heading-tick-{labelIndex}", string.Empty, Vector2.Zero, visible: false);
    }

    // The centre marker and the heading readout under it
    var marker = new Vector3(center.X, center.Y - 0.1f, 0f);

    shapes.DrawPixelLine(marker, marker + new Vector3(-0.22f, -0.3f, 0f), ThickLine, theme.Text);
    shapes.DrawPixelLine(marker, marker + new Vector3(0.22f, -0.3f, 0f), ThickLine, theme.Text);

    Style(ThinLine, 0.9f, theme.Fill);
    ChamferedPanel(new Vector2(center.X, center.Y - 0.85f), new Vector2(1.6f, 0.62f), 0.12f, theme.Accent);
    SetLabel("heading", $"{((ship.Heading % 360f) + 360f) % 360f:000}", new Vector2(center.X, center.Y - 0.85f));

    // Three little mode glyph panels above the tape, as on the reference frame
    foreach (var (dx, glyph) in new[] { (-4.4f, "///"), (0f, "<>"), (4.4f, "^") })
    {
        Style(ThinLine, 0.6f, theme.Fill);
        ChamferedPanel(new Vector2(center.X + dx, top + 0.95f), new Vector2(1.9f, 0.5f), 0.1f, theme.Accent);
        SetLabel($"glyph-{glyph}", glyph, new Vector2(center.X + dx, top + 0.95f));
    }

    _ = LabelHeight;
}

/// <summary>The gun-sight: two rings, four gaps, a dot.</summary>
void DrawReticle(Vector2 center, Theme theme)
{
    Style(ThinLine, 0f);

    // Four arcs with gaps at the cardinal points read as a sight; a full ring reads as a target
    for (var i = 0; i < 4; i++)
    {
        var start = i * MathF.PI / 2f + 0.25f;

        shapes!.DrawArc(center, 0.42f, start, MathF.PI / 2f - 0.5f, theme.Text);
    }

    shapes!.DrawSolidCircle(center, 0.035f, theme.Text);

    foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
    {
        var from = new Vector3(center.X + dx * 0.5f, center.Y + dy * 0.5f, 0f);
        var to = new Vector3(center.X + dx * 0.75f, center.Y + dy * 0.75f, 0f);

        shapes.DrawPixelLine(from, to, ThinLine, theme.Text);
    }
}

/// <summary>
/// Pitch lines every ten degrees, sliding vertically with the ship's pitch. Positive lines are
/// solid and negative ones broken, the convention every aircraft HUD shares.
/// </summary>
void DrawPitchLadder(Vector2 center, Theme theme)
{
    const float UnitsPerDegree = 0.115f;
    const float Window = 3.4f;

    Style(ThinLine, 0f);

    for (var pitch = -30; pitch <= 30; pitch += 10)
    {
        var y = center.Y + (pitch - ship.Pitch) * UnitsPerDegree;
        var visible = pitch != 0 && MathF.Abs(y - center.Y) < Window && MathF.Abs(y - center.Y) > 0.6f;

        SetLabel($"pitch-{pitch}", $"{pitch:+0;-0}", new Vector2(center.X + 3.1f, y), visible);
        SetLabel($"pitch-left-{pitch}", $"{pitch:+0;-0}", new Vector2(center.X - 3.1f, y), visible);

        if (!visible) continue;

        var arm = 1.4f;
        var gap = 0.9f;

        // Below the horizon the lines are dashed: the same two lines, with a dash pattern on
        Style(ThinLine, 0f, dash: pitch > 0 ? 0f : 9f, gap: 6f);

        shapes!.DrawPixelLine(new Vector3(center.X - arm - gap, y, 0f), new Vector3(center.X - gap, y, 0f), ThinLine, theme.Accent);
        shapes.DrawPixelLine(new Vector3(center.X + gap, y, 0f), new Vector3(center.X + arm + gap, y, 0f), ThinLine, theme.Accent);
    }
}

/// <summary>
/// A vertical tape with a moving fill, a threshold mark and the value boxed beside it - the VEL
/// and ALT tapes either side of the sight.
/// </summary>
void DrawVerticalGauge(Vector2 center, float height, float value, string key, Theme theme)
{
    const float Width = 0.42f;

    value = MathUtil.Clamp(value, 0f, 1f);

    var bottom = center.Y - height / 2f;
    var top = center.Y + height / 2f;

    // Track
    Style(ThinLine, 0.25f, theme.Fill);
    shapes!.DrawRectangle(new Vector3(center, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(Width, height), theme.Dim);

    // Fill, growing from the bottom
    Style(0f, 0.85f, theme.Accent);
    shapes.DrawRectangle(new Vector3(center.X, bottom + height * value / 2f, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(Width - 0.08f, height * value), theme.Accent);

    // Ticks up the outside, a longer one every fifth
    Style(ThinLine, 0f);

    var side = center.X < 0f ? 1f : -1f;

    for (var i = 0; i <= 20; i++)
    {
        var y = bottom + height * i / 20f;
        var length = i % 5 == 0 ? 0.3f : 0.15f;

        shapes.DrawPixelLine(new Vector3(center.X + side * Width / 2f, y, 0f), new Vector3(center.X + side * (Width / 2f + length), y, 0f), ThinLine, theme.Accent);
    }

    // The red-line threshold, as a small hollow box on the tape's edge
    var thresholdY = bottom + height * 0.88f;

    Style(ThinLine, 0f);
    shapes.DrawRectangle(new Vector3(center.X - side * (Width / 2f + 0.16f), thresholdY, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(0.22f, 0.22f), Theme.Danger);

    // The readout follows the fill
    var readoutY = MathUtil.Clamp(bottom + height * value, bottom + 0.35f, top - 0.35f);
    var readoutCenter = new Vector2(center.X + side * 1.05f, readoutY);

    Style(ThinLine, 0.95f, theme.Fill);
    ChamferedPanel(readoutCenter, new Vector2(1.3f, 0.56f), 0.1f, theme.Accent);
    SetLabel(key, key == "speed" ? $"{ship.Speed:0}" : $"{ship.Altitude:0}", readoutCenter);

    // Caption above the tape
    Style(ThinLine, 0.6f, theme.Fill);
    ChamferedPanel(new Vector2(center.X, top + 0.5f), new Vector2(1.2f, 0.5f), 0.1f, theme.Accent);
    SetLabel($"{key}-caption", key == "speed" ? "VEL" : "ALT M", new Vector2(center.X, top + 0.5f));
}

/// <summary>
/// Range rings, a cross-hair, a sweep that leaves a fading wedge behind it, and the contacts it
/// finds. The rings are drawn as broken arcs - a dozen short arcs each - which is what a dashed
/// ring costs until the shader can dash one for us.
/// </summary>
void DrawRadar(Vector2 center, float radius, Theme theme)
{
    // Ground disc
    Style(ThinLine, 0.5f, theme.Fill);
    shapes!.DrawSolidCircle(center, radius, theme.Accent);

    // Range rings, dashed - one shape each
    DashedRing(center, radius * 0.66f, 7f, 6f, theme.Dim, 0f);
    DashedRing(center, radius * 0.33f, 7f, 6f, theme.Dim, 0f);

    Style(ThinLine, 0f);
    shapes.DrawPixelLine(new Vector3(center.X - radius, center.Y, 0f), new Vector3(center.X + radius, center.Y, 0f), ThinLine, theme.Dim);
    shapes.DrawPixelLine(new Vector3(center.X, center.Y - radius, 0f), new Vector3(center.X, center.Y + radius, 0f), ThinLine, theme.Dim);

    // The sweep: a wedge that fades across its width. Three sectors of decreasing alpha stacked
    // behind the leading edge is a fair fake of a gradient until the shader has one.
    var sweep = ship.RadarSweep;

    for (var i = 0; i < 3; i++)
    {
        Style(0f, 0.16f - i * 0.05f, theme.Accent);
        shapes.DrawSector(center, radius - 0.03f, sweep - (i + 1) * 0.35f, 0.35f, theme.Accent);
    }

    Style(ThinLine, 0f);
    shapes.DrawPixelLine(new Vector3(center, 0f), new Vector3(center.X + MathF.Cos(sweep) * radius, center.Y + MathF.Sin(sweep) * radius, 0f), ThickLine, theme.Accent);

    // Contacts: friendly in the accent, the one hostile in red with a ring around it
    for (var i = 0; i < ship.Contacts.Length; i++)
    {
        var contact = ship.Contacts[i];
        var position = center + new Vector2(MathF.Cos(contact.Angle), MathF.Sin(contact.Angle)) * contact.Distance * radius;
        var colour = contact.Hostile ? Theme.Danger : theme.Text;

        Style(0f, 1f, colour);
        shapes.DrawSolidCircle(position, 0.07f, colour);

        if (contact.Hostile)
        {
            Style(ThinLine, 0f);
            shapes.DrawArc(position, 0.18f, 0f, MathF.Tau, colour);
        }
    }

    // Bezel, and a slowly turning tick ring outside it: the phase advancing is the whole animation
    Style(ThickLine, 0f, null, 4f, theme.Glow);
    shapes.DrawArc(center, radius, 0f, MathF.Tau, theme.Accent);
    DashedRing(center, radius + 0.22f, 4f, 10f, theme.Dim, time * 12f);

    SetLabel("radar-caption", "RADAR  5 KM", new Vector2(center.X, center.Y + radius + 0.42f));
}

/// <summary>
/// The chat-style log in the corner: a framed panel with a header strip and six lines that scroll
/// as messages arrive. A panel inside a panel, which is how a real HUD groups things.
/// </summary>
void DrawCommsPanel(Vector2 center, Vector2 size, Theme theme)
{
    Style(ThinLine, 0.55f, theme.Fill);
    ChamferedPanel(center, size, 0.25f, theme.Accent);

    // Header strip inside the frame
    var headerCenter = new Vector2(center.X, center.Y + size.Y / 2f - 0.32f);

    Style(0f, 0.9f, theme.Accent);
    shapes!.DrawRectangle(new Vector3(headerCenter, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(size.X - 0.5f, 0.42f), theme.Accent);
    SetLabel("comms-header", "PROX COMMS", headerCenter);

    // Lines, newest at the bottom, the oldest fading out
    var visibleLines = 6;
    var first = ship.CommsIndex;

    for (var i = 0; i < visibleLines; i++)
    {
        var message = commsLog[(first + i) % commsLog.Length];
        var y = headerCenter.Y - 0.62f - i * 0.62f;

        SetLabel($"comms-{i}", message, new Vector2(center.X - size.X / 2f + 0.4f, y));
    }
}

/// <summary>
/// A ring gauge: a dim track, a bright arc that grows clockwise from the top, a tick ring outside
/// it and the figure in the middle. Three of them along the bottom, one of which turns amber.
/// </summary>
void DrawRingGauge(Vector2 center, float radius, float value, string key, Color colour, Theme theme)
{
    value = MathUtil.Clamp(value, 0f, 1f);

    // Track
    Style(0f, 0.35f, theme.Fill);
    shapes!.DrawAnnulus(center, radius, radius - 0.22f, theme.Dim);

    // Progress, clockwise from twelve o'clock, square-ended
    Style(0f, 0.95f, colour, 3f, colour);
    shapes.DrawSector(center, radius, MathF.PI / 2f, -MathF.Tau * value, colour, radius - 0.22f);

    // Tick ring outside, and the bezel
    DashedRing(center, radius + 0.18f, 3f, 5f, theme.Dim, 0f);

    Style(ThinLine, 0f);
    shapes.DrawArc(center, radius + 0.02f, 0f, MathF.Tau, theme.Dim);

    SetLabel(key, $"{value * 100f:0}%", center + new Vector2(0f, 0.08f));
    SetLabel($"{key}-caption", key.ToUpperInvariant(), center - new Vector2(0f, radius + 0.42f));
}

/// <summary>A framed trace of the last few seconds of a signal, drawn as a chain of pixel lines.</summary>
void DrawSparkline(Vector2 center, Vector2 size, Theme theme)
{
    Style(ThinLine, 0.45f, theme.Fill);
    ChamferedPanel(center, size, 0.18f, theme.Accent);

    var left = center.X - size.X / 2f + 0.25f;
    var width = size.X - 0.5f;
    var bottom = center.Y - size.Y / 2f + 0.2f;
    var height = size.Y - 0.6f;
    var samples = ship.Trace;

    Style(ThinLine, 0f);

    for (var i = 1; i < samples.Length; i++)
    {
        var from = new Vector3(left + width * (i - 1) / (samples.Length - 1), bottom + height * samples[i - 1], 0f);
        var to = new Vector3(left + width * i / (samples.Length - 1), bottom + height * samples[i], 0f);

        shapes!.DrawPixelLine(from, to, ThinLine, theme.Text);
    }

    // Baseline
    shapes!.DrawPixelLine(new Vector3(left, bottom, 0f), new Vector3(left + width, bottom, 0f), ThinLine, theme.Dim);

    SetLabel("trace-caption", "PMT", new Vector2(center.X - size.X / 2f + 0.5f, center.Y + size.Y / 2f - 0.22f));
}

/// <summary>A dozen bars from a moving spectrum, each one rectangle, the tallest in the text colour.</summary>
void DrawBarChart(Vector2 center, Vector2 size, Theme theme)
{
    Style(ThinLine, 0.45f, theme.Fill);
    ChamferedPanel(center, size, 0.18f, theme.Accent);

    var bars = ship.Spectrum;
    var left = center.X - size.X / 2f + 0.3f;
    var width = size.X - 0.6f;
    var bottom = center.Y - size.Y / 2f + 0.2f;
    var height = size.Y - 0.6f;
    var pitch = width / bars.Length;

    for (var i = 0; i < bars.Length; i++)
    {
        var barHeight = MathF.Max(0.04f, height * bars[i]);
        var colour = bars[i] > 0.85f ? theme.Text : theme.Accent;

        Style(0f, 0.9f, colour);
        shapes!.DrawRectangle(new Vector3(left + pitch * (i + 0.5f), bottom + barHeight / 2f, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(pitch * 0.6f, barHeight), colour);
    }

    SetLabel("spectrum-caption", "IN", new Vector2(center.X - size.X / 2f + 0.4f, center.Y + size.Y / 2f - 0.22f));
}

/// <summary>
/// The panel with a header and three labelled bars. Hull, shield and fuel: the shield is the one
/// that moves, and the one whose colour is not the theme's when it is low.
/// </summary>
void DrawSystemsPanel(Vector2 center, Vector2 size, Theme theme)
{
    Style(ThinLine, 0.55f, theme.Fill);
    ChamferedPanel(center, size, 0.25f, theme.Accent);

    var headerCenter = new Vector2(center.X, center.Y + size.Y / 2f - 0.3f);

    Style(0f, 0.9f, theme.Accent);
    shapes!.DrawRectangle(new Vector3(headerCenter, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(size.X - 0.5f, 0.4f), theme.Accent);
    SetLabel("systems-header", "SYSTEMS", headerCenter);

    var rows = new[] { ("HULL", ship.Hull, theme.Accent), ("SHIELD", ship.Shield, ship.Shield < 0.35f ? Theme.Danger : theme.Accent), ("FUEL", ship.Fuel, ship.Fuel < 0.2f ? Theme.Warning : theme.Accent) };

    for (var i = 0; i < rows.Length; i++)
    {
        var (name, value, colour) = rows[i];
        var y = headerCenter.Y - 0.62f - i * 0.58f;
        var barLeft = center.X - size.X / 2f + 2.0f;
        var barWidth = size.X - 3.6f;

        SetLabel($"systems-{name}", name, new Vector2(center.X - size.X / 2f + 0.4f, y));
        SegmentedBar(new Vector2(barLeft + barWidth / 2f, y), new Vector2(barWidth, 0.3f), 20, value, colour, theme);
        SetLabel($"systems-{name}-value", $"{value * 100f:0}%", new Vector2(center.X + size.X / 2f - 0.5f, y));
    }
}

/// <summary>
/// Amber when something needs attention, red when it needs it now, and quietly nominal the rest of
/// the time. The one panel whose colour the theme never decides.
/// </summary>
void DrawWarningStrip(Vector2 center, Theme theme)
{
    var size = new Vector2(5.4f, 0.62f);
    var pulse = 0.5f + 0.5f * MathF.Sin(time * 6f);

    if (ship.Shield < 0.35f)
    {
        var colour = ship.Shield < 0.15f ? Theme.Danger : Theme.Warning;

        // The panel is the colour; the lettering is dark on it, the way a lit annunciator reads
        Style(ThinLine, 0.55f + 0.35f * pulse, colour, 6f * pulse, colour);
        ChamferedPanel(center, size, 0.14f, colour);
        SetLabel("warning", ship.Shield < 0.15f ? "SHIELD CRITICAL" : "SHIELD LOW", center, colour: new Color(12, 8, 8));

        return;
    }

    Style(ThinLine, 0.3f, theme.Fill);
    ChamferedPanel(center, size, 0.14f, theme.Dim);
    SetLabel("warning", "ALL SYSTEMS NOMINAL", center, colour: theme.Dim);
}

/// <summary>
/// The bracket that follows a target: four corners with a gap between them, breathing on a sine,
/// and the range under it. The one widget that moves across the whole sight.
/// </summary>
void DrawTargetBox(Theme theme)
{
    var center = ship.Target;
    var half = 0.5f + 0.05f * MathF.Sin(time * 4f);
    var arm = 0.22f;

    Style(ThinLine, 0f, null, 3f, theme.Glow);

    foreach (var (sx, sy) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
    {
        var corner = new Vector3(center.X + sx * half, center.Y + sy * half, 0f);

        shapes!.DrawPixelLine(corner, corner - new Vector3(sx * arm, 0f, 0f), ThickLine, theme.Text);
        shapes.DrawPixelLine(corner, corner - new Vector3(0f, sy * arm, 0f), ThickLine, theme.Text);
    }

    SetLabel("target", $"TGT  {ship.TargetRange:0.0} KM", center - new Vector2(0f, half + 0.3f));
}

/// <summary>
/// One of four status tiles on the right: a name, a count and a segmented bar. The selected one is
/// bright and glows; the idle ones are dim. Selected against idle is the pair every UI needs, and it
/// is all in three numbers - border width, fill alpha and glow.
/// </summary>
void DrawStatusTile(Vector2 center, Vector2 size, int index, Theme theme)
{
    var selected = index == selectedTile;
    var pulse = 0.5f + 0.5f * MathF.Sin(time * 3f);
    var value = ship.TileValues[index];

    if (selected)
    {
        Style(ThickLine, 0.75f, theme.Fill, 4f + 4f * pulse, theme.Glow);
        ChamferedPanel(center, size, 0.16f, theme.Accent);
    }
    else
    {
        Style(ThinLine, 0.45f, theme.Fill);
        ChamferedPanel(center, size, 0.16f, theme.Dim);
    }

    var colour = selected ? theme.Text : theme.Accent;

    SetLabel($"tile-{index}", tileNames[index], new Vector2(center.X - size.X / 2f + 0.35f, center.Y + 0.2f), colour: colour);
    SetLabel($"tile-{index}-value", $"{value * 63f:0} / 63", new Vector2(center.X - size.X / 2f + 0.35f, center.Y - 0.2f), colour: colour);

    SegmentedBar(new Vector2(center.X + size.X / 2f - 1.35f, center.Y), new Vector2(2.0f, 0.26f), 10, value, colour, theme);
}

/// <summary>
/// The buttons along the bottom right: one active, one disabled at 35%, the rest idle. Disabled is
/// every colour at a fraction of its alpha, which is the case that argues for an opacity on the batch.
/// </summary>
void DrawModeButton(Vector2 center, Vector2 size, int index, Theme theme)
{
    var active = index == 1;
    var disabled = index == 0;
    var opacity = disabled ? 0.35f : 1f;

    // Active is filled solid in the accent with dark lettering - inverted, the way a lit button is
    Style(active ? ThickLine : ThinLine, active ? 0.9f : 0.5f, WithAlpha(active ? theme.Accent : theme.Fill, opacity), active ? 4f : 0f, theme.Glow);
    ChamferedPanel(center, size, 0.16f, WithAlpha(active ? theme.Text : theme.Accent, opacity));

    var caption = index == 3 ? $"{modeNames[index]}\n{ship.Decoys}" : modeNames[index];

    SetLabel($"mode-{index}", caption, center, colour: WithAlpha(active ? theme.Fill : theme.Text, opacity));
}

// --- Primitives the widgets are built from ---------------------------------------------------

/// <summary>A rectangle with its corners cut at 45 degrees - the HUD panel shape - as one convex polygon.</summary>
void ChamferedPanel(Vector2 center, Vector2 size, float cut, Color colour)
{
    var w = size.X / 2f;
    var h = size.Y / 2f;

    cut = MathF.Min(cut, MathF.Min(w, h));

    ReadOnlySpan<Vector2> corners =
    [
        new(-w + cut, -h), new(w - cut, -h), new(w, -h + cut), new(w, h - cut),
        new(w - cut, h), new(-w + cut, h), new(-w, h - cut), new(-w, -h + cut),
    ];

    shapes!.DrawSolidPolygon(corners, center, 0f, colour);
}

/// <summary>
/// A dashed ring - a tick ring, a range ring, a dial's scale - as one shape. The dash and gap are
/// pixels, the same at any zoom; the batch fits a whole number of them round the turn, so it never
/// ends in a stub. The phase is pixels too: advance it and the ring turns.
/// </summary>
void DashedRing(Vector2 center, float radius, float dashPixels, float gapPixels, Color colour, float phasePixels)
{
    Style(ThinLine, 0f, dash: dashPixels, gap: gapPixels, phase: phasePixels);
    shapes!.DrawArc(center, radius, 0f, MathF.Tau, colour);
}

/// <summary>A bar made of cells, lit up to the value: the reference packs' "bar download".</summary>
void SegmentedBar(Vector2 center, Vector2 size, int cells, float value, Color colour, Theme theme)
{
    var pitch = size.X / cells;
    var left = center.X - size.X / 2f;
    var lit = (int)MathF.Round(MathUtil.Clamp(value, 0f, 1f) * cells);

    for (var i = 0; i < cells; i++)
    {
        var on = i < lit;

        Style(0f, on ? 0.95f : 0.25f, on ? colour : theme.Dim);
        shapes!.DrawRectangle(new Vector3(left + pitch * (i + 0.5f), center.Y, 0f), Vector3.UnitX, Vector3.UnitY, new Vector2(pitch * 0.7f, size.Y), on ? colour : theme.Dim);
    }
}

/// <summary>Sets the whole of the batch's captured state at once, so no draw inherits a stale value.</summary>
void Style(float border, float fillAlpha, Color? fill = null, float glow = 0f, Color? glowColour = null, float dash = 0f, float gap = 0f, float phase = 0f)
{
    shapes!.BorderWidth = border;
    shapes.FillAlpha = fillAlpha;
    shapes.FillColor = fill;
    shapes.GlowWidth = glow;
    shapes.GlowColor = glowColour;
    shapes.DashLength = dash;
    shapes.DashGap = gap;
    shapes.DashPhase = phase;
}

// --- Text ------------------------------------------------------------------------------------

/// <summary>
/// Every text component, created once with its font and size, so each frame only moves it and
/// sets its string. Anything a theme decides is set through the restyle delegate that
/// <see cref="ApplyTheme"/> re-runs.
/// </summary>
void CreateLabels(Scene scene)
{
    for (var i = 0; i < 10; i++) AddLabel(scene, $"heading-tick-{i}", 0.26f, monoFont, (t, th) => t.TextColor = th.Accent);

    AddLabel(scene, "heading", 0.34f, monoFont, (t, th) => { t.TextColor = th.Text; t.GlowColor = th.Glow; t.GlowSize = 3f; });

    foreach (var glyph in new[] { "///", "<>", "^" }) AddLabel(scene, $"glyph-{glyph}", 0.28f, boldFont, (t, th) => t.TextColor = th.Accent);

    for (var pitch = -30; pitch <= 30; pitch += 10)
    {
        AddLabel(scene, $"pitch-{pitch}", 0.27f, monoFont, (t, th) => t.TextColor = th.Accent);
        AddLabel(scene, $"pitch-left-{pitch}", 0.27f, monoFont, (t, th) => t.TextColor = th.Accent);
    }

    foreach (var key in new[] { "speed", "altitude" })
    {
        AddLabel(scene, key, 0.32f, monoFont, (t, th) => t.TextColor = th.Text);
        AddLabel(scene, $"{key}-caption", 0.24f, boldFont, (t, th) => t.TextColor = th.Accent);
    }

    AddLabel(scene, "radar-caption", 0.24f, boldFont, (t, th) => t.TextColor = th.Accent);
    AddLabel(scene, "comms-header", 0.26f, boldFont, (t, th) => t.TextColor = th.Fill);

    for (var i = 0; i < 6; i++)
    {
        var index = i;

        // Older lines dimmer: alpha carries the age
        AddLabel(scene, $"comms-{i}", 0.27f, sansFont, (t, th) => t.TextColor = WithAlpha(th.Text, 0.55f + 0.45f * index / 5f), TextAnchor.MiddleLeft);
    }

    foreach (var key in new[] { "power", "temperature", "thrust" })
    {
        AddLabel(scene, key, 0.36f, monoFont, (t, th) => { t.TextColor = th.Text; t.GlowColor = th.Glow; t.GlowSize = 3f; });
        AddLabel(scene, $"{key}-caption", 0.22f, boldFont, (t, th) => t.TextColor = th.Accent);
    }

    AddLabel(scene, "trace-caption", 0.22f, boldFont, (t, th) => t.TextColor = th.Accent, TextAnchor.MiddleLeft);
    AddLabel(scene, "spectrum-caption", 0.22f, boldFont, (t, th) => t.TextColor = th.Accent, TextAnchor.MiddleLeft);
    AddLabel(scene, "systems-header", 0.26f, boldFont, (t, th) => t.TextColor = th.Fill);

    foreach (var name in new[] { "HULL", "SHIELD", "FUEL" })
    {
        AddLabel(scene, $"systems-{name}", 0.24f, boldFont, (t, th) => t.TextColor = th.Accent, TextAnchor.MiddleLeft);
        AddLabel(scene, $"systems-{name}-value", 0.24f, monoFont, (t, th) => t.TextColor = th.Text, TextAnchor.MiddleRight);
    }

    // Colour set per frame by the widget, not by the theme
    AddLabel(scene, "warning", 0.28f, boldFont, (_, _) => { }, glow: 4f);
    AddLabel(scene, "target", 0.24f, monoFont, (t, th) => t.TextColor = th.Text);

    for (var i = 0; i < tileNames.Length; i++)
    {
        AddLabel(scene, $"tile-{i}", 0.24f, boldFont, (_, _) => { }, TextAnchor.MiddleLeft);
        AddLabel(scene, $"tile-{i}-value", 0.24f, monoFont, (_, _) => { }, TextAnchor.MiddleLeft);
    }

    for (var i = 0; i < modeNames.Length; i++)
    {
        AddLabel(scene, $"mode-{i}", 0.24f, boldFont, (_, _) => { });
    }
}

void AddLabel(Scene scene, string key, float lineHeight, SpriteFont? font, Action<WorldTextComponent, Theme> restyle, TextAnchor anchor = TextAnchor.MiddleCenter, float glow = 0f)
{
    var component = new WorldTextComponent
    {
        Text = string.Empty,
        Height = lineHeight,
        FontSize = 48,
        Font = font,
        Anchor = anchor,
        Alignment = TextAlignment.Center,
        GlowSize = glow,
        DepthTest = false,
    };

    restyle(component, themes[themeIndex]);

    var entity = new Entity(key) { component };

    entity.Scene = scene;

    labels[key] = new Label(entity, component, lineHeight);
    themedText.Add((component, restyle));
}

/// <summary>Moves a label and sets its string; a colour given here overrides whatever the theme set.</summary>
void SetLabel(string key, string text, Vector2 position, bool visible = true, Color? colour = null)
{
    var label = labels[key];

    label.Text.IsVisible = visible;

    if (!visible) return;

    if (label.Text.Text != text)
    {
        label.Text.Text = text;

        // Height is the whole block, so a second line must not halve the letters
        label.Text.Height = label.LineHeight * (text.Count(c => c == '\n') + 1);
    }

    if (colour is { } c) label.Text.TextColor = c;

    label.Entity.Transform.Position = new Vector3(position, 0f);
}

void ApplyTheme(int index)
{
    themeIndex = index;

    foreach (var (text, restyle) in themedText)
    {
        restyle(text, themes[themeIndex]);
    }
}

IReadOnlyList<TextElement> OverlayLines()
{
    List<TextElement> lines =
    [
        new($"{lastShapeCount} shapes in one draw call, {labels.Count} labels", Color.LightGreen),
        new(paused ? "SPACE - resume" : "SPACE - freeze the ship", Color.Yellow),
        new("TAB - select the next status tile", Color.Yellow),
        new(string.Empty),
    ];

    if (themeMenu is not null) lines.AddRange(themeMenu.GetLines());

    return lines;
}

static Color WithAlpha(Color colour, float alpha) => new(colour.R, colour.G, colour.B, (byte)(colour.A * MathUtil.Clamp(alpha, 0f, 1f)));

/// <summary>A palette. Warning and danger are fixed: amber and red mean the same in every scheme.</summary>
sealed record Theme(string Name, Color Accent, Color Fill, Color Text, Color Glow)
{
    public static readonly Color Warning = new(255, 176, 40);
    public static readonly Color Danger = new(255, 64, 56);

    /// <summary>The accent at a third of its strength: tracks, idle frames, range rings.</summary>
    public Color Dim => new(Accent.R, Accent.G, Accent.B, (byte)90);
}

/// <summary>A text component with the entity that positions it and the size one line of it is.</summary>
sealed record Label(Entity Entity, WorldTextComponent Text, float LineHeight);

/// <summary>A radar contact: where it is, in polar terms, and whether it is on our side.</summary>
readonly record struct Contact(float Angle, float Distance, bool Hostile);

/// <summary>
/// The ship that flies itself: every figure is a function of time, which is what lets SPACE freeze
/// it by freezing the clock. Nothing here is physics; it is whatever makes each widget move
/// through its whole range within a minute.
/// </summary>
sealed class ShipState
{
    public float Speed;
    public float Altitude;
    public float Heading;
    public float Pitch;
    public float Hull = 0.87f;
    public float Shield;
    public float Fuel;
    public float Power;
    public float Temperature;
    public float Thrust;
    public float RadarSweep;
    public Vector2 Target;
    public float TargetRange;
    public int CommsIndex;
    public int Decoys = 48;
    public readonly float[] Trace = new float[64];
    public readonly float[] Spectrum = new float[12];
    public readonly float[] TileValues = [1f, 0.62f, 0.87f, 0.3f];
    public readonly Contact[] Contacts = new Contact[5];

    private float _lastTraceTime;

    public void Advance(float time)
    {
        Speed = 150f + 90f * MathF.Sin(time * 0.35f) + 15f * MathF.Sin(time * 2.1f);
        Altitude = 1100f + 600f * MathF.Sin(time * 0.21f + 1f);
        Heading = 150f + 40f * MathF.Sin(time * 0.17f) + 6f * MathF.Sin(time * 0.9f);
        Pitch = 8f * MathF.Sin(time * 0.4f) + 3f * MathF.Sin(time * 1.3f);

        // The shield takes a hit every 20 seconds and recharges over the next ten
        var cycle = time % 20f;
        Shield = cycle < 10f ? 1f - cycle / 10f * 0.9f : 0.1f + (cycle - 10f) / 10f * 0.9f;

        Fuel = 0.9f - (time % 240f) / 240f * 0.8f;
        Power = 0.55f + 0.35f * MathF.Sin(time * 0.6f);
        Temperature = 0.5f + 0.4f * MathF.Sin(time * 0.25f + 2f);
        Thrust = MathUtil.Clamp(Speed / 300f + 0.1f * MathF.Sin(time * 3f), 0f, 1f);
        RadarSweep = -time * 1.4f;

        Target = new Vector2(6.0f + 1.2f * MathF.Sin(time * 0.5f), 3.6f + 0.8f * MathF.Sin(time * 0.33f + 1f));
        TargetRange = 1.2f + 0.5f * MathF.Sin(time * 0.3f);

        CommsIndex = (int)(time / 3f);
        Decoys = 48 - (int)(time / 15f) % 20;

        for (var i = 0; i < Contacts.Length; i++)
        {
            var angle = time * (0.08f + 0.04f * i) + i * 1.3f;
            var distance = 0.3f + 0.6f * (0.5f + 0.5f * MathF.Sin(time * 0.15f + i * 2f));

            Contacts[i] = new Contact(angle, distance, Hostile: i == 0);
        }

        // The trace scrolls: every tenth of a second the oldest sample falls off the left
        if (time - _lastTraceTime > 0.1f)
        {
            _lastTraceTime = time;

            Array.Copy(Trace, 1, Trace, 0, Trace.Length - 1);
            Trace[^1] = MathUtil.Clamp(0.5f + 0.3f * MathF.Sin(time * 4f) + 0.15f * MathF.Sin(time * 13f) + 0.05f * MathF.Sin(time * 41f), 0f, 1f);
        }

        for (var i = 0; i < Spectrum.Length; i++)
        {
            Spectrum[i] = MathUtil.Clamp(0.45f + 0.4f * MathF.Sin(time * (1.5f + i * 0.3f) + i) + 0.1f * MathF.Sin(time * 9f + i * 3f), 0.02f, 1f);
        }

        TileValues[1] = 0.5f + 0.5f * MathF.Sin(time * 0.7f);
    }
}

/*
---example-metadata
slug: hud
title:
  en: Ship HUD
  cs: HUD lodi
level: Intermediate
category: Shapes
complexity: 3
order: 76
description:
  en: |-
    A cockpit HUD composed from the toolkit's shapes and world text: a heading tape that scrolls, a
    pitch ladder, a gun-sight, speed and altitude tapes with moving readouts, a radar with a sweep
    and contacts, ring gauges, a sparkline and a spectrum, a comms log in a framed panel, four status
    tiles with a selected one that glows, mode buttons with one disabled, and a warning strip that
    goes amber and then red as the shield drops. Every shape is one draw call; five colour schemes
    switch live; the ship flies itself so every widget moves.
  cs: |-
    HUD kokpitu složený z tvarů a textu ve světě: rolující páska kurzu, žebřík sklonu, zaměřovač,
    pásky rychlosti a výšky s pohyblivými údaji, radar s paprskem a kontakty, kruhové ukazatele,
    křivka signálu a spektrum, komunikační log v orámovaném panelu, čtyři stavové dlaždice s jednou
    vybranou a zářící, tlačítka režimů s jedním vypnutým a varovný pruh, který se šieldem zežloutne
    a pak zčervená. Všechny tvary v jednom volání; pět barevných schémat lze přepínat za běhu; loď
    letí sama, takže se každý prvek hýbe.
concepts:
  - Composing a HUD from ShapeBatch panels, bars, arcs, sectors and pixel lines in one draw call
  - Framed panels with chamfered corners as a single convex polygon
  - Selected against idle, disabled, and warning states as border, fill alpha and glow
  - Dashed rings and a fading sweep built from arcs and sectors, pending shader support
  - World text updated in place, reused for scrolling tape labels
  - A theme that leaves warning and danger colours alone
  - A simulated ship as functions of time, frozen by freezing the clock
tags:
  - 2D
  - Shapes
  - Text
  - HUD
  - Themes
  - Gauges
  - Radar
related:
  - Example03_2DScene_Panels
  - Example01_WorldText
  - Example_Shapes_Playground
enabled: true
created: 2026-09-06
---
*/