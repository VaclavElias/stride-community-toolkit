---
generated: true
slug: 2d-scene-panels
---

# 2D Panels and Text

Sixteen HUD panel recipes side by side, each one property away from the last: fill only, border
only, transparent fill over a stripe that proves it, a fill colour of its own, rounded corners,
heavy borders, glows of three strengths and colours, glass, a ship-console panel with corner
ticks and a gauge, dashed rings and lines that turn and march, a gradient to the text colour, a
gradient to nothing, and a panel at a third opacity. Every panel appears twice - alone, and
carrying world text that demonstrates height, font size, colour alpha, opacity, glow and system
fonts in regular, bold, italic and monospace. Five themes switch live.

The `Program.cs` file shows how to:

- Panels with ShapeBatch - border, fill, glow, dashes, gradient and opacity as captured state
- A fill colour of its own versus a fill derived from the outline colour
- Showing transparency honestly by drawing a stripe behind every panel
- Dashes on rings and lines, animated by advancing the phase
- A fill gradient to a colour, and to alpha 0 for a glass fade
- One opacity over a whole panel
- Pixel-width lines and arcs as HUD ornaments that survive zooming
- World text styling - Height, FontSize, TextColor alpha, Opacity, GlowColor and GlowSize
- Installed system fonts with SystemFonts, in four styles
- A live theme switch through a DebugOverlay dropdown

View on [GitHub](https://github.com/stride3d/stride-community-toolkit/tree/main/examples/code-only/E03_2D_Panels).

[!code-csharp[](../../../../examples/code-only/E03_2D_Panels/Program.cs?start=1&end=463)]
