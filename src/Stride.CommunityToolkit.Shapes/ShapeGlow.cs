using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// A soft glow outside a shape's outline. <see cref="ShapeBatch.Glow"/> holds one; each draw call
/// captures its values as it is made.
/// </summary>
/// <remarks>
/// The glow lies outside the shape only, never under the fill or the border, and fades out
/// quadratically from the border's outer edge over <see cref="Width"/> pixels - constant at any
/// distance, like the border. For a
/// stroke-only ring or arc the shape is the stroke, so the glow sits on both sides of it, which is
/// what makes a light ring with a dark glow readable on any background.
/// </remarks>
/// <example>
/// <code>
/// shapes.Glow.Set(8f, new Color(0, 0, 0, 200));   // a dark halo
/// shapes.DrawRing(cursor, Vector3.UnitY, 0.9f, Color.White);
/// shapes.Glow.Clear();
/// </code>
/// </example>
public sealed class ShapeGlow
{
    /// <summary>
    /// Width of the glow in on-screen pixels. The default 0 draws none. A few pixels reads as a
    /// crisp halo and a few dozen as a neon bloom.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// The glow's colour, or <c>null</c> (the default) to glow in the outline colour. Its alpha is
    /// the glow's strength at the outline, before the fade - and it is the number that decides what
    /// the glow reads as. At full alpha the halo is solid colour where it meets the edge and only
    /// then falls off, so a thin stroke looks like a fatter stroke; at 30 to 40 percent it reads as
    /// light around the stroke, which is the neon look. Start there.
    /// </summary>
    public Color? Color { get; set; }

    /// <summary>
    /// Whether the glow adds its light to whatever is behind it rather than covering it. Off by
    /// default: a glow then behaves like a soft, translucent halo that can darken as well as
    /// lighten. On, it is a neon bloom that only ever brightens - a black glow adds nothing.
    /// </summary>
    public bool Additive { get; set; }

    /// <summary>Sets both at once.</summary>
    /// <param name="width">Width in pixels; 0 for none.</param>
    /// <param name="color">The glow colour, or <c>null</c> for the outline colour.</param>
    public void Set(float width, Color? color = null)
    {
        Width = width;
        Color = color;
    }

    /// <summary>No glow, back to the outline colour, and not additive.</summary>
    public void Clear()
    {
        Set(0f);
        Additive = false;
    }
}