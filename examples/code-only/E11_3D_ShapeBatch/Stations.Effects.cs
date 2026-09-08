using static E11_3D_ShapeBatch.Palette;
using Stride.Core.Mathematics;

namespace E11_3D_ShapeBatch;

// The per-draw states a shape can wear, and the batches a shape can go through.
public static class EffectStations
{
    /// <summary>
    /// Three ways to fill a disc. The first derives its fill from the outline colour, dimmed by the
    /// fill alpha, the Box2D testbed way; the second has a fill colour of its own inside a white
    /// outline; the third has no border at all, a fill and nothing else.
    /// </summary>
    public static void FillColour(GalleryStation s)
    {
        var shapes = s.Shapes;

        shapes.Fill.Set(null, 0.45f);
        shapes.DrawDisc(s.At(-3.6f, Lift, 0f), s.Up, 1.5f, Color.Orange);

        shapes.Fill.Set(new Color(20, 40, 110), 1f);
        shapes.DrawDisc(s.At(0f, Lift, 0f), s.Up, 1.5f, Color.White);

        shapes.BorderWidth = 0f;
        shapes.Fill.Set(Color.Crimson, 1f);
        shapes.DrawDisc(s.At(3.6f, Lift, 0f), s.Up, 1.5f, Color.White);

        s.ResetStyle(shapes);
    }

    /// <summary>
    /// The glow lives outside the outline and fades over a pixel width, so it neither tints the
    /// fill nor changes with distance. Its best use is contrast: a white cursor ring on a dark halo
    /// stays readable over anything. Wide and in the shape's own colour it is neon - standing here
    /// as a ring, a disc whose glow stops at its edge, and an arc. Press G to glow every station.
    /// </summary>
    public static void Glow(GalleryStation s)
    {
        var shapes = s.Shapes;
        var (sin, cos) = MathF.SinCos(s.Seconds * 0.6f);
        var cursor = s.At(cos * 3f, Lift, 2f + sin * 1.2f);

        shapes.Glow.Set(8f, new Color(0, 0, 0, 200));
        shapes.DrawRing(cursor, s.Up, 0.7f, Color.White);
        shapes.DrawPixelLine(cursor - s.Right * 1.3f, cursor + s.Right * 1.3f, 1.5f, Color.White);
        shapes.DrawPixelLine(cursor - s.Forward * 1.3f, cursor + s.Forward * 1.3f, 1.5f, Color.White);

        shapes.Glow.Set(28f);
        shapes.DrawRing(s.At(0f, 2.6f, -1f), s.Forward, 1.2f, Color.Cyan);
        shapes.DrawDisc(s.At(-3.4f, 2.6f, -1f), s.Forward, 0.9f, Color.Magenta);
        shapes.DrawArc(s.At(3.4f, 2.6f, -1f), s.Forward, 1.2f, s.Seconds * 1.5f, MathF.PI * 1.2f, Color.OrangeRed);

        s.ResetStyle(shapes);
    }

    /// <summary>
    /// Dashes are measured in pixels like the border and belong to rings, arcs and lines; advancing
    /// the phase turns a ring or marches a line. Three dashed rings turning at their own speeds and
    /// dash-to-gap ratios - tight ticks, half and half, sparse dots - the middle one with an
    /// additive glow, the right one breathing through its opacity; behind them two tick rings
    /// turning against each other.
    /// </summary>
    public static void Dash(GalleryStation s)
    {
        var shapes = s.Shapes;

        ReadOnlySpan<(float Dash, float Gap, float Speed)> rings = [(6f, 4f, 30f), (10f, 10f, -20f), (3f, 12f, 45f)];

        for (var i = 0; i < rings.Length; i++)
        {
            var (dash, gap, speed) = rings[i];
            var centre = s.At(-3.6f + i * 3.6f, Lift, 1.5f);

            shapes.Dash.Set(dash, gap, s.Seconds * speed);

            switch (i)
            {
                case 0:
                    shapes.BorderWidth = 2f;
                    shapes.DrawRing(centre, s.Up, 1.4f, Color.Cyan);
                    break;
                case 1:
                    shapes.BorderWidth = 3f;
                    shapes.Glow.Set(8f, new Color(255, 140, 0, 150));
                    shapes.Glow.Additive = true;
                    shapes.DrawRing(centre, s.Up, 1.4f, Color.Orange);
                    shapes.Glow.Clear();
                    break;
                default:
                    shapes.BorderWidth = 5f;
                    shapes.Opacity = 0.55f + 0.45f * MathF.Sin(s.Seconds * 2f);
                    shapes.DrawRing(centre, s.Up, 1.4f, Color.GreenYellow);
                    shapes.Opacity = 1f;
                    break;
            }
        }

        shapes.BorderWidth = s.Style.BorderWidth;

        shapes.Dash.Set(8f, 6f, s.Seconds * 25f);
        shapes.DrawRing(s.At(0f, Lift, -2f), s.Up, 2.6f, Color.Orange);
        shapes.Dash.Set(4f, 10f, -s.Seconds * 40f);
        shapes.DrawRing(s.At(0f, Lift, -2f), s.Up, 3.2f, Color.Orange);
        shapes.Dash.Clear();
    }

    /// <summary>
    /// A gradient runs the fill from its colour to another across the shape's own extent: a bar
    /// filling to its bright end, and a glass pane that fades to nothing along its length. The
    /// direction is in the plane's own axes, so X runs along the station's right.
    /// </summary>
    public static void Gradient(GalleryStation s)
    {
        var shapes = s.Shapes;
        var bar = (MathF.Sin(s.Seconds * 0.9f) + 1f) * 0.5f;

        shapes.BorderWidth = 1.5f;
        shapes.Fill.Set(new Color(255, 120, 40, 110), 1f);
        shapes.Gradient.Set(new Color(255, 230, 120), Vector2.UnitX);
        shapes.DrawRectangle(s.At(-3f + 3f * bar, 1f, 0f), s.Right, s.Up, new Vector2(6f * bar, 0.7f), Color.Orange);
        shapes.Gradient.Clear();

        shapes.Fill.Set(new Color(120, 200, 255, 140), 1f);
        shapes.Gradient.Set(new Color(120, 200, 255, 0), Vector2.UnitX);
        shapes.DrawRectangle(s.At(0f, 3.4f, 0f), s.Right, s.Up, new Vector2(8f, 2.4f), new Color(120, 200, 255), cornerRadius: 0.3f);

        s.ResetStyle(shapes);
    }

    /// <summary>
    /// Opacity dims everything a shape draws with one number: the same glowing billboard at 1, 0.65
    /// and 0.3, border, fill and glow together. It is how a widget goes disabled or fades in.
    /// </summary>
    public static void Opacity(GalleryStation s)
    {
        var shapes = s.Shapes;

        shapes.Glow.Set(10f);

        for (var i = 0; i < 3; i++)
        {
            shapes.Opacity = 1f - i * 0.35f;
            shapes.DrawBillboardCircle(s.At(-3.6f + i * 3.6f, 3f, 0f), 0.8f, Color.Gold);
        }

        s.ResetStyle(shapes);
    }

    /// <summary>
    /// The soft depth fade. A shape fades out over a distance as it approaches scene geometry
    /// instead of cutting off at the depth test: a ring standing a hand in front of the pillar dims
    /// over the pillar and stays bright beside it; a marker sunk in the floor melts into it, next
    /// to the same marker without the fade, sliced flat. Only shows on the depth-tested batch.
    /// </summary>
    public static void DepthFade(GalleryStation s)
    {
        var shapes = s.Shapes;
        var pillar = s.Pillars[0];

        shapes.DepthFade = 0.6f;
        shapes.DrawRing(pillar.Base + Vector3.UnitY * 2.2f + s.Forward * 1.1f, s.Forward, 1.5f, Color.LightGreen);

        shapes.DepthFade = 1.5f;
        shapes.DrawBillboardCircle(s.At(1.6f, 0.5f, 0.5f), 0.9f, Color.Gold);

        shapes.DepthFade = 0f;
        shapes.DrawBillboardCircle(s.At(4.2f, 0.5f, 0.5f), 0.9f, Color.Gold);
    }

    /// <summary>
    /// Two batches, from two calls to AddShapeBatch. The depth-tested one belongs to the scene: its
    /// cyan ring behind the pillar is hidden by it. The overlay one draws over everything: its pink
    /// ring in the same place shows through the pillar, which is what gizmos and debug marks want.
    /// T switches every other station between the two; this one draws through both at once.
    /// </summary>
    public static void OverlayBatch(GalleryStation s)
    {
        var pillar = s.Pillars[0];
        var behind = pillar.Base + Vector3.UnitY * 2f - s.Forward * 2f;

        s.Batches.Scene.DrawRing(behind + s.Right * 0.7f, s.Forward, 1.4f, Color.Cyan);
        s.Batches.Overlay.DrawRing(behind - s.Right * 0.7f, s.Forward, 1.4f, Color.HotPink);
    }

    /// <summary>
    /// A textured batch: a batch created with a fill source samples it for every shape drawn while
    /// Textured is on. The gallery's picture, generated in code with a white square at its top-left
    /// so the mapping is unmistakable, fills a glowing HUD panel edge to edge, a disc with its
    /// inscribed square, and a disc tinted orange - the sample multiplies the fill colour.
    /// </summary>
    public static void TexturedFill(GalleryStation s)
    {
        var shapes = s.Batches.Pictures;

        shapes.Fill.Set(Color.White, 1f);
        shapes.Glow.Set(7f, HudGlow);
        shapes.DrawRectangle(s.At(-2.5f, 2.4f, 0f), s.Right, s.Up, new Vector2(5f, 3.5f), HudBlue, cornerRadius: 0.5f);
        shapes.Glow.Clear();

        shapes.DrawDisc(s.At(2.2f, 2.1f, 0f), s.Forward, 1.5f, Color.Cyan);

        shapes.Fill.Set(new Color(255, 160, 60), 1f);
        shapes.DrawDisc(s.At(4.6f, 1.2f, 1f), s.Up, 1.1f, Color.White);

        s.ResetStyle(shapes);
    }

    /// <summary>
    /// The same picture through a batch whose fill source tiles it four times across with wrap
    /// addressing. The node is re-read every frame, so scrolling is one assignment to its offset.
    /// </summary>
    public static void ScrollingTexture(GalleryStation s)
    {
        var shapes = s.Batches.Stripes;

        s.Batches.Stripe.Offset = new Vector2(s.Seconds * 0.25f, 0f);

        shapes.BorderWidth = 2f;
        shapes.Fill.Set(Color.White, 1f);
        shapes.DrawRectangle(s.At(0f, 1.4f, 0f), s.Right, s.Up, new Vector2(10f, 1.6f), Color.White, cornerRadius: 0.2f);

        s.ResetStyle(shapes);
    }
}