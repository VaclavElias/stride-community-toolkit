using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Shapes;

/// <summary>
/// A soft glow outside a shape's outline. <see cref="ShapeBatch.Glow"/> holds one; each draw call
/// captures its values as it is made.
/// </summary>
/// <remarks>
/// The glow lies outside the shape only, never under the fill, and fades out quadratically from the
/// outline over <see cref="Width"/> pixels - constant at any distance, like the border. For a
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
    /// the glow's strength at the outline, before the fade.
    /// </summary>
    public Color? Color { get; set; }

    /// <summary>Sets both at once.</summary>
    /// <param name="width">Width in pixels; 0 for none.</param>
    /// <param name="color">The glow colour, or <c>null</c> for the outline colour.</param>
    public void Set(float width, Color? color = null)
    {
        Width = width;
        Color = color;
    }

    /// <summary>No glow, and back to the outline colour.</summary>
    public void Clear() => Set(0f);
}