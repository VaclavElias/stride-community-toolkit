using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Shapes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Graphics.Font;
using Stride.Input;

// A gallery of HUD panels and the text on them: twelve stations, each one property away from the
// last, so what every setting does is a matter of looking rather than reading.
//
// Two libraries meet here and it is worth knowing which is which. The PANELS are ShapeBatch - one
// instanced draw call for all of them, outlines that stay the same number of pixels wide however far
// you zoom, and border, fill, glow and corner radius as batch state captured per draw. The TEXT is
// WorldTextComponent - entities in the scene, drawn by one renderer, with their own colour, opacity,
// glow and font.
//
// Each station appears twice: the panel alone, and the same panel with text on it. A light stripe
// runs behind every cell, because a see-through panel over a flat background just looks darker.
//
// T opens the theme list; 1-5 pick one. The mouse wheel zooms and the right button pans - zoom in on
// any station to see how little the borders and glows care about it.

const int Columns = 6;
const float ColumnPitch = 5f;
const float RowPitch = 3.4f;
const float PanelWidth = 4.4f;
const float PanelHeight = 2.2f;
const float ViewHeight = 21f;

// The grid sits low on purpose. The overlay is screen-space and lives in the top-right corner, so the
// band above the grid is what keeps it from covering the last column.
const float GridOffsetY = -3.2f;

// The whole grid framed at once; every cell is one wheel notch away from filling the window
var panelSize = new Vector2(PanelWidth, PanelHeight);

// The stripe behind each cell, slanted so it is obvious it belongs to the background
var stripeAxisX = Vector3.Normalize(new Vector3(1f, 0.42f, 0f));
var stripeAxisY = Vector3.Normalize(new Vector3(-0.42f, 1f, 0f));

ShapeBatch? shapes = null;
SpriteFont? sansFont = null;
SpriteFont? boldFont = null;
SpriteFont? monoFont = null;

var themeIndex = 0;
List<(WorldTextComponent Text, Action<WorldTextComponent, Theme> Restyle)> themedText = [];
DebugTextDropdown? themeMenu = null;

// Dark grounds throughout: a glow is light added to what is behind it, so it only reads as a glow
// against something dark. That is why every HUD in every spaceship is dark.
Theme[] themes =
[
    new("Blue", new Color(90, 190, 255), new Color(10, 26, 48), new Color(205, 238, 255), new Color(0, 140, 255)),
    new("Red", new Color(255, 95, 90), new Color(38, 12, 14), new Color(255, 210, 205), new Color(255, 40, 40)),
    new("Green", new Color(80, 245, 150), new Color(8, 34, 22), new Color(200, 255, 225), new Color(0, 220, 120)),
    new("Purple", new Color(185, 130, 255), new Color(26, 14, 46), new Color(230, 210, 255), new Color(150, 60, 255)),
    new("Orange", new Color(255, 170, 70), new Color(42, 22, 6), new Color(255, 228, 190), new Color(255, 130, 0)),
];

// One entry per station: how the panel is painted, and how the text on the copy below it is styled.
// Panel settings are ShapeBatch state; text settings are WorldTextComponent properties.
Station[] stations =
[
    new("Fill only\nBorderWidth 0", "Default text",
        (text, theme) => text.TextColor = theme.Text,
        BorderWidth: 0f),

    new("Border only\nFillAlpha 0", "Height 0.55",
        (text, theme) => { text.TextColor = theme.Text; text.Height = 0.55f; },
        FillAlpha: 0f),

    new("Border + fill\nthe default look", "FontSize 12\n(zoom in - it goes soft)",
        (text, theme) => { text.TextColor = theme.Text; text.FontSize = 12f; }),

    new("FillAlpha 0.35\nthe stripe shows through", "TextColor alpha 128",
        (text, theme) => text.TextColor = new Color(theme.Text.R, theme.Text.G, theme.Text.B, (byte)128),
        FillAlpha: 0.35f),

    new("FillColor null\nfill derives from the border", "Opacity 0.5",
        (text, theme) => { text.TextColor = theme.Text; text.Opacity = 0.5f; },
        DerivedFill: true, FillAlpha: 0.45f),

    new("cornerRadius 0.35\nrounded", "GlowSize 4\ncrisp halo",
        (text, theme) => { text.TextColor = theme.Text; text.GlowColor = theme.Glow; text.GlowSize = 4f; },
        CornerRadius: 0.35f),

    new("BorderWidth 6\nheavy, still pixel-exact", "GlowSize 12, glow alpha 90\nsoft bloom",
        (text, theme) => { text.TextColor = theme.Text; text.GlowColor = new Color(theme.Glow.R, theme.Glow.G, theme.Glow.B, (byte)90); text.GlowSize = 12f; },
        BorderWidth: 6f),

    new("GlowWidth 6\nin the accent colour", "Dark text, white halo\nlegible on anything",
        (text, _) => { text.TextColor = new Color(10, 12, 18); text.GlowColor = Color.White; text.GlowSize = 7f; },
        GlowWidth: 6f),

    new("GlowWidth 14, glow alpha 70\nwide and weak", "System sans font",
        (text, theme) => { text.TextColor = theme.Text; text.Font = sansFont; },
        GlowWidth: 14f, GlowAlpha: 70),

    new("GlowColor white\nindependent of the border", "System sans, bold",
        (text, theme) => { text.TextColor = theme.Text; text.Font = boldFont; },
        GlowWidth: 5f, WhiteGlow: true),

    new("Glass\nFillAlpha 0.18 + rounded + glow", "Monospace font\n0123456789",
        (text, theme) => { text.TextColor = theme.Text; text.Font = monoFont; },
        FillAlpha: 0.18f, CornerRadius: 0.3f, GlowWidth: 5f),

    new("The lot, plus ticks and a gauge\n(the ship-HUD panel)", "DOCKING CLAMP\nSTATUS   NOMINAL",
        (text, theme) => { text.TextColor = theme.Text; text.Font = boldFont; text.GlowColor = theme.Glow; text.GlowSize = 5f; },
        BorderWidth: 1.5f, FillAlpha: 0.55f, CornerRadius: 0.25f, GlowWidth: 6f, Ornaments: true),
];

using var game = new Game();

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.Window.AllowUserResizing = true;

    // No ground and no physics: this is a gallery, and the default 2D ground would cut through it
    game.SetupBase2D(new Color(6, 8, 14));
    game.Add2DCameraController();
    game.AddWorldTextRenderer();

    shapes = game.AddShapeBatch();

    var camera = game.GetCameraEntity().Get<CameraComponent>();

    // The controller reads this on its first frame and returns to it when H resets the camera
    camera.OrthographicSize = ViewHeight;

    // A code-only game has no font assets, so any font other than Stride's default one comes from
    // the machine. Asking for a list rather than a name is what keeps this working off Windows;
    // game.LoadSystemFont("Segoe UI", 48) is the one-family version.
    sansFont = SystemFonts.LoadFirst(game.Services, SystemFonts.SansSerifCandidates, 48);
    boldFont = SystemFonts.LoadFirst(game.Services, SystemFonts.SansSerifCandidates, 48, FontStyle.Bold);
    monoFont = SystemFonts.LoadFirst(game.Services, SystemFonts.MonospaceCandidates, 48);

    // Fills the band the overlay does not reach, and is itself a station: a big glowing title is
    // exactly what the text component is for
    AddText(scene, new Vector3(-6.2f, 6.2f, 0f), "PANELS  &  TEXT", 0.9f,
        (text, theme) =>
        {
            text.TextColor = theme.Text;
            text.GlowColor = theme.Glow;
            text.GlowSize = 6f;
            text.Font = boldFont;
        });

    for (var i = 0; i < stations.Length; i++)
    {
        var station = stations[i];
        var panelCenter = CellCenter(i, withText: false);

        // Under the panel-only copy, in a neutral grey that belongs to no theme, so it reads as a
        // caption rather than as part of the panel
        AddText(scene, panelCenter - new Vector3(0f, PanelHeight / 2f + 0.42f, 0f), station.Caption, 0.26f,
            (text, _) =>
            {
                text.TextColor = new Color(150, 160, 175);

                // The halo is not decoration here: a caption sits over whatever glow the panel above
                // it throws, and a tight dark outline is what keeps grey text readable on both
                text.GlowColor = new Color(0, 0, 0, 200);
                text.GlowSize = 3f;
            },
            themed: false);

        // On the copy below it, the text names the setting that styles it - the station is its own label
        AddText(scene, CellCenter(i, withText: true), station.Label, 0.34f, station.StyleText);
    }

    themeMenu = new DebugTextDropdown
    {
        Title = "Theme",
        ToggleKey = Keys.T,
        TitleColor = Color.Yellow,
        SelectedIndex = themeIndex,
        CloseOnSelect = false,
        IsOpen = true,
        Items = [.. themes.Index().Select(pair => new DebugTextDropdownItem(
            (Keys)(Keys.D1 + pair.Index), pair.Item.Name, () => ApplyTheme(pair.Index)))],
    };

    DebugOverlay.GetOrCreate(game).AddSection("Panels", OverlayLines);
}

void Update(Scene scene, GameTime time)
{
    if (shapes is null) return;

    themeMenu?.Update(game.Input);

    var theme = themes[themeIndex];

    // Immediate mode: every panel is submitted again each frame, which is why a theme change needs
    // nothing more than reading the new colours here. All of them go out in one draw call.
    for (var i = 0; i < stations.Length; i++)
    {
        DrawPanel(stations[i], CellCenter(i, withText: false), theme);
        DrawPanel(stations[i], CellCenter(i, withText: true), theme);
    }
}

/// <summary>Paints one panel: the stripe behind it, the panel itself, and any HUD ornaments.</summary>
void DrawPanel(Station station, Vector3 center, Theme theme)
{
    // The stripe is what makes transparency visible. Drawn first: shapes blend in submission order
    // and never write depth, so within a batch "behind" simply means "earlier".
    shapes!.BorderWidth = 0f;
    shapes.FillAlpha = 1f;
    shapes.FillColor = null;
    shapes.GlowWidth = 0f;
    shapes.GlowColor = null;
    // Kept just inside the panel, slant included, so it never pokes out of a cell and gets mistaken
    // for part of the panel
    shapes.DrawRectangle(center, stripeAxisX, stripeAxisY, new Vector2(PanelWidth * 0.85f, 0.5f), new Color(118, 130, 150));

    shapes.BorderWidth = station.BorderWidth;
    shapes.FillAlpha = station.FillAlpha;

    // null fills with the outline colour, which is the Box2D testbed's behaviour; a colour of its own
    // is what makes a dark panel behind a bright border
    shapes.FillColor = station.DerivedFill ? null : theme.Fill;

    shapes.GlowWidth = station.GlowWidth;
    shapes.GlowColor = station.GlowWidth <= 0f ? null : WithAlpha(station.WhiteGlow ? Color.White : theme.Glow, station.GlowAlpha);

    shapes.DrawRectangle(center, Vector3.UnitX, Vector3.UnitY, panelSize, theme.Accent, station.CornerRadius);

    if (station.Ornaments)
    {
        DrawOrnaments(center, theme);
    }
}

/// <summary>
/// The corner ticks, divider and gauge arc that turn a rounded rectangle into something off a ship's
/// console. Lines in pixel widths, so the ornament keeps its weight as you zoom.
/// </summary>
void DrawOrnaments(Vector3 center, Theme theme)
{
    const float TickLength = 0.55f;

    var half = new Vector2(PanelWidth / 2f + 0.16f, PanelHeight / 2f + 0.16f);

    shapes!.BorderWidth = 0f;
    shapes.FillAlpha = 1f;
    shapes.FillColor = null;
    shapes.GlowWidth = 0f;
    shapes.GlowColor = null;

    foreach (var (signX, signY) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
    {
        var corner = center + new Vector3(signX * half.X, signY * half.Y, 0f);

        shapes.DrawPixelLine(corner, corner - new Vector3(signX * TickLength, 0f, 0f), 2f, theme.Accent);
        shapes.DrawPixelLine(corner, corner - new Vector3(0f, signY * TickLength, 0f), 2f, theme.Accent);
    }

    var divider = center + new Vector3(0f, -PanelHeight / 2f + 0.55f, 0f);

    shapes.DrawPixelLine(
        divider - new Vector3(PanelWidth / 2f - 0.35f, 0f, 0f),
        divider + new Vector3(PanelWidth / 2f - 0.35f, 0f, 0f),
        1f,
        WithAlpha(theme.Accent, 110));

    // A gauge sweep, three quarters of a turn, in the accent colour
    shapes.DrawArc(
        new Vector2(center.X + PanelWidth / 2f - 0.55f, center.Y - PanelHeight / 2f + 0.55f),
        0.3f,
        -MathF.PI / 2f,
        MathF.PI * 1.5f,
        theme.Accent,
        0.05f);
}

/// <summary>Creates one text entity, styled through the same delegate a theme change re-runs.</summary>
void AddText(Scene scene, Vector3 position, string content, float lineHeight, Action<WorldTextComponent, Theme> restyle, bool themed = true)
{
    var component = new WorldTextComponent
    {
        Text = content,
        // Height is the whole block, so scaling it by the line count keeps one-line and two-line
        // stations at the same letter size - the point of a comparison gallery
        Height = lineHeight * (content.Count(character => character == '\n') + 1),
        FontSize = 48,
        Alignment = TextAlignment.Center,
    };

    restyle(component, themes[themeIndex]);

    var entity = new Entity("Text") { component };

    entity.Transform.Position = position;
    entity.Scene = scene;

    if (themed)
    {
        themedText.Add((component, restyle));
    }
}

/// <summary>Switches theme: panels re-read it by themselves, text has to be re-styled.</summary>
void ApplyTheme(int index)
{
    themeIndex = index;

    var theme = themes[themeIndex];

    foreach (var (text, restyle) in themedText)
    {
        restyle(text, theme);
    }
}

/// <summary>Where a station sits: columns across, and a panel row with its text row under it.</summary>
Vector3 CellCenter(int station, bool withText)
{
    var column = station % Columns;
    var group = station / Columns;
    var row = group * 2 + (withText ? 1 : 0);

    return new Vector3(
        (column - (Columns - 1) / 2f) * ColumnPitch,
        (1.5f - row) * RowPitch + GridOffsetY,
        0f);
}

IReadOnlyList<TextElement> OverlayLines()
{
    List<TextElement> lines =
    [
        new("Panels: ShapeBatch, one draw call for all 24", Color.LightGreen),
        new("Text: WorldTextComponent, one entity each", Color.LightGreen),
        new("Upper row of each pair is the panel alone", Color.LightGray),
        new("Wheel zooms - borders and glows keep their pixel width", Color.LightGray),
        new(string.Empty),
    ];

    if (themeMenu is not null)
    {
        lines.AddRange(themeMenu.GetLines());
    }

    return lines;
}

/// <summary>The same colour at a different alpha.</summary>
static Color WithAlpha(Color color, byte alpha) => new(color.R, color.G, color.B, alpha);

/// <summary>A palette: one accent, one dark ground, one text colour and one glow.</summary>
sealed record Theme(string Name, Color Accent, Color Fill, Color Text, Color Glow);

/// <summary>
/// One station of the gallery: a panel recipe, the caption naming it, and the text drawn on the
/// second copy of the panel with the styling that names itself.
/// </summary>
sealed record Station(
    string Caption,
    string Label,
    Action<WorldTextComponent, Theme> StyleText,
    float BorderWidth = 2f,
    float FillAlpha = 1f,
    float CornerRadius = 0f,
    float GlowWidth = 0f,
    byte GlowAlpha = 255,
    bool DerivedFill = false,
    bool WhiteGlow = false,
    bool Ornaments = false);

/*
---example-metadata
slug: 2d-scene-panels
title:
  en: 2D Panels and Text
  cs: 2D panely a text
level: Beginner
category: Shapes
complexity: 2
order: 75
description:
  en: |-
    Twelve HUD panel recipes side by side, each one property away from the last: fill only, border
    only, transparent fill over a stripe that proves it, a fill colour of its own, rounded corners,
    heavy borders, glows of three strengths and colours, glass, and a ship-console panel with corner
    ticks and a gauge. Every panel appears twice - alone, and carrying world text that demonstrates
    height, font size, colour alpha, opacity, glow and system fonts. Five themes switch live.
  cs: |-
    Dvanáct receptů na HUD panely vedle sebe, každý o jednu vlastnost dál: jen výplň, jen okraj,
    průhledná výplň nad pruhem, který to dokáže, vlastní barva výplně, zaoblené rohy, silné okraje,
    záře tří sil a barev, sklo a panel lodní konzole s rohovými značkami a ukazatelem. Každý panel je
    dvakrát - samotný a s textem ve světě, který ukazuje výšku, velikost písma, alfu barvy,
    průhlednost, záři a systémová písma. Pět motivů lze přepínat za běhu.
concepts:
  - Panels with ShapeBatch - border, fill, fill alpha, corner radius and glow as captured state
  - A fill colour of its own versus a fill derived from the outline colour
  - Showing transparency honestly by drawing a stripe behind every panel
  - Pixel-width lines and arcs as HUD ornaments that survive zooming
  - World text styling - Height, FontSize, TextColor alpha, Opacity, GlowColor and GlowSize
  - Installed system fonts with SystemFonts and game.LoadSystemFont
  - A live theme switch through a DebugOverlay dropdown
tags:
  - 2D
  - Shapes
  - Text
  - HUD
  - Panels
  - Glow
  - Transparency
  - Fonts
  - Themes
related:
  - Example01_WorldText
  - Example_Shapes_Playground
  - Example01_EntityText
enabled: true
created: 2026-09-05
---
*/