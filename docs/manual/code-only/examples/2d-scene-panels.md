---
generated: true
slug: 2d-scene-panels
---

# 2D Panels and Text

Twelve HUD panel recipes side by side, each one property away from the last: fill only, border
only, transparent fill over a stripe that proves it, a fill colour of its own, rounded corners,
heavy borders, glows of three strengths and colours, glass, and a ship-console panel with corner
ticks and a gauge. Every panel appears twice - alone, and carrying world text that demonstrates
height, font size, colour alpha, opacity, glow and system fonts. Five themes switch live.

The `Program.cs` file shows how to:

- Panels with ShapeBatch - border, fill, fill alpha, corner radius and glow as captured state
- A fill colour of its own versus a fill derived from the outline colour
- Showing transparency honestly by drawing a stripe behind every panel
- Pixel-width lines and arcs as HUD ornaments that survive zooming
- World text styling - Height, FontSize, TextColor alpha, Opacity, GlowColor and GlowSize
- Installed system fonts with SystemFonts and game.LoadSystemFont
- A live theme switch through a DebugOverlay dropdown

View on [GitHub](https://github.com/stride3d/stride-community-toolkit/tree/main/examples/code-only/Example03_2DScene_Panels).

[!code-csharp[](../../../../examples/code-only/Example03_2DScene_Panels/Program.cs?start=1&end=381)]
