# Debug Overlay

`DebugOverlay` is one block of on-screen text, assembled from **sections** contributed by whatever has
something to say - the camera controller's key help, your own instructions, a live counter - with one
position and one hide key for the lot. It is a game system, so it survives scene swaps and draws
itself every frame; nothing calls it.

The tempting alternative is `game.DebugTextSystem.Print(...)` from every script's `Update`. That works
for one line. With three scripts each printing at a hand-picked pixel position, the lines overlap on
the first window resize, the camera help draws on top of your counter, and every one of them is 8 by 16
pixel text that a 4K display makes unreadable. The overlay exists so that text has one owner.

`DebugOverlay`, `DebugOverlaySection`, `TextElement` and `DisplayPosition` live in the
`Stride.CommunityToolkit.Scripts.Utilities` namespace.

## Adding text

Get the shared instance and add a section. The callback runs every frame the overlay is drawn, so
values that change need no pushing - this is `E02_3D_Primitives`:

```csharp
var overlay = DebugOverlay.GetOrCreate(game);

overlay.Position = DisplayPosition.BottomLeft;

overlay.AddSection("Game", static () =>
[
    new("INSTRUCTIONS"),
    new("Press P to see collidables"),
    new("Press F11 to see debug meshes"),
    new("Press R to reset the scene", Color.Yellow),
]);
```

Each `TextElement` is a line and an optional colour; a line without one uses `DefaultTextColor`.
Sections are separated by a blank line and sorted by `order`. The 3D camera controller registers its
help at order `-100`, so anything added without an order lands below it.

A section can collapse to a single title line and expand again on a key:

```csharp
overlay.AddCollapsibleSection("Physics", "Physics", Keys.F5, () => [ ... ], collapsed: true);
```

That is how `F2 - Camera controls [+]` works. `AddSection` returns the `DebugOverlaySection`, so a
section can be disabled, collapsed or removed later.

> [!TIP]
> Something that already knows how to describe itself as lines - `DebugTextDropdown.GetLines()`, for
> example - plugs straight in: `overlay.AddSection("Spawn", () => spawnMenu.GetLines())`. That is what
> keeps the spawn menu in `E04_2D_SpawnMenu` in the same block as the camera help
> instead of being a second patch of text somewhere else.

## Keys and position

| Key or property | Default | Does |
|---|---|---|
| `ToggleKey` | <kbd>F4</kbd> | Hides and shows the whole overlay. |
| `RepositionKey` | <kbd>F3</kbd> | Cycles the corner: top-left, top-right, bottom-right, bottom-left. |
| `Position` | `TopRight` | The corner. `DisplayPosition.Custom` uses `CustomPosition` instead; `None` draws nothing. |
| `Margin` | `5, 10` | Distance from the corner, in unscaled pixels. |

Section keys are read even while the overlay is hidden, so a collapse toggle pressed with everything
off still takes effect.

> [!NOTE]
> The camera controllers claim <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd>, <kbd>Q</kbd> <kbd>E</kbd>,
> the arrow keys, <kbd>H</kbd>, <kbd>F2</kbd> and <kbd>F3</kbd>. Bind section toggles and example
> keys elsewhere; safe single letters include <kbd>G</kbd> <kbd>J</kbd> <kbd>K</kbd> <kbd>L</kbd>
> <kbd>M</kbd> <kbd>N</kbd> <kbd>P</kbd> <kbd>R</kbd> <kbd>T</kbd> <kbd>Z</kbd>.

## Size, and high-DPI displays

The overlay draws with a real font, rasterised at the size asked for, so it can be any size and is
sharp at every one of them. It follows the display by default: on a laptop at 150% it draws one and
a half times larger than on a monitor at 100%, so it reads the same size to the eye on both. The
factor comes from `DisplayScale`, which is shared with everything else that draws in pixels and is
re-read when the window moves to a differently scaled monitor. Three properties control the rest:

- **`AutoScale`** is that behaviour, on by default. Turn it off to draw at exactly `Scale`, for a
  screenshot at a known size or when the game applies its own UI-scale setting.
- **`Scale`** multiplies everything - text, line spacing, margins, padding - on top of the display's
  factor, so the block keeps its layout. It is a preference, "a bit bigger", not a DPI figure:
  `1.25` on a 150% display draws at 1.875.
- **`FontSize`** is the text height at scale 1 on a 100% display, in pixels. Defaults to 16, the
  height of Stride's own debug text.

The display factor is only right if the process is DPI aware - otherwise Windows is already
stretching the whole window and the factor correctly reads 1. Declare awareness in an
`app.manifest`, as the Stride templates do, or call `WindowsDpiManager.EnablePerMonitorV2()` from
the `Stride.CommunityToolkit.Windows` package before the game is created; see the
[DPI-aware example](../code-only/examples/dpi-aware.md).

Why not just scale Stride's debug text? Because it is a bitmap: an 8 by 16 pixel glyph sheet that
`DebugTextSystem` draws at exactly that size, with a grey strip baked behind every glyph. The first
attempt at enlarging it - a transform over the small texture - came out blurred; the second - doubling
every pixel - came out sharp but blocky next to the vector text everywhere else on a 4K desktop. A
rasterised font is what makes debug text look like the rest of the screen, and it is what the toolkit's
[entity text](entity-text.md) already used.

## Fonts

Stride's default font asset is **bold**. The overlay looks for an installed system font instead,
picked by `FontFamily`:

| `FontFamily` | Windows | macOS | Linux |
|---|---|---|---|
| `Monospace` (default) | Consolas, Cascadia Mono, Courier New | Menlo, Courier New | DejaVu Sans Mono, Liberation Mono, Courier New |
| `SansSerif` | Segoe UI, Arial | Helvetica, Arial | Liberation Sans, DejaVu Sans, Arial |

The first family found in the system font folders wins. Monospace is the default for the same reason
Stride's own debug font is fixed-width: columns line up, a number does not shift its neighbours as its
digits change, and text aligned with spaces keeps its shape - `E10_3D_Instancing_EntityTransform`
lays out its comparison table with `{count,6}` and would fall apart in a proportional font.

To be specific rather than pick a family:

- `FontName = "Cascadia Mono"` names an installed family; `FontStyle` chooses `Regular`, `Bold`,
  `Italic` or `BoldItalic`.
- `FontFile = "/path/to/font.ttf"` points at a file that is not in the system font folders.
- `Font = someSpriteFont` supplies a font asset of your own and overrides all of the above.
- `FontName = null` with a family none of whose fonts are installed falls back to Stride's default
  font. So does any font that fails to load - with one line in the log saying which font was wanted,
  never a blank overlay.

The lookup finds files by the common naming conventions (`consola.ttf`, `LiberationMono-Regular.ttf`,
`DejaVuSansMono.ttf`); it does not consult fontconfig, so an unusually named file needs `FontFile`.

## Background

Each line gets a strip behind it, exactly as wide as its text:

| Property | Default | Notes |
|---|---|---|
| `BackgroundColor` | black at 49% alpha | The look of Stride's debug text. `Color.Transparent` draws no strip. |
| `BackgroundPadding` | `3, 1` | How far the strip extends beyond the text, in unscaled pixels. |
| `DefaultTextColor` | `LightGreen` | For lines that give no colour. |
| `TitleColor` | `null` | Section titles; `null` means `DefaultTextColor`. |

On a light scene the default strip is too faint to carry light-coloured text. Darken it, or invert
the scheme:

```csharp
// The charts playground, paper-white 2D scene
overlay.BackgroundColor = new Color(0, 0, 0, 200);

// Or a light strip with dark text
overlay.BackgroundColor = new Color(255, 255, 255, 230);
overlay.DefaultTextColor = Color.Black;
```

The strip is drawn by the overlay, not by the font. This is worth knowing only because Stride's own
debug text is the other way round - its strip is baked into the glyphs at a fixed 49% black and cannot
be changed, which is why a text-only fix was never going to make it readable on white.

## Line spacing

`LineHeight` is `null` by default, which means the distance between lines is worked out from the
font: text height, plus the padding above and below, plus `LineSpacing` (default `2` pixels). Change
`FontSize` and the lines follow. `LineSpacing = 0` makes the strips touch; a fixed `LineHeight` in
unscaled pixels overrides the calculation entirely.

## What it is not

- **Not part of the graphics compositor.** It draws straight to the back buffer after everything else,
  so it is always on top and unaffected by post effects, and it needs nothing registered in the
  compositor.
- **Not the `DebugTextDropdown.Draw(DebugTextSystem)` path.** A dropdown drawn standalone that way
  still uses Stride's bitmap text at its own position; hand its `GetLines()` to the overlay instead to
  get the font, scale, background and hide key.
- **Not a UI toolkit.** One block of lines in a corner. Anything laid out in the scene wants
  [entity text](entity-text.md) or [world text](world-text.md); anything interactive wants Stride UI
  or the ImGui package.

## Where it is used in the toolkit

| Piece | In `E11_2D_Charts` | Concept |
|---|---|---|
| `DebugOverlay.GetOrCreate(game)` | `Program.cs`, `Start` | one shared instance, registered as a service |
| `WindowsDpiManager.EnablePerMonitorV2()` | `Program.cs`, before `new Game()` | a sharp window; the overlay's size then follows `DisplayScale` on its own |
| `overlay.BackgroundColor = new Color(0, 0, 0, 200)` | `Program.cs`, 2D branch | readable on a white scene |
| `overlay.AddSection("Chart", () => [...])` | `Program.cs` | a live line: `Press G to toggle the grid (on)` |
| `Add3DCameraController()` | via `SetupBase3DScene` | the collapsible `F2 - Camera controls` section, order `-100` |
