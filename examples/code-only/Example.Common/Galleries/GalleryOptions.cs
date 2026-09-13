namespace Example.Common.Galleries;

/// <summary>
/// The few figures a gallery's layout depends on, with the values the shape gallery settled on.
/// </summary>
/// <param name="Spacing">Arc length per station, in world units: a pad, and a gap to the next one.</param>
/// <param name="MinimumRadius">The ring is never tighter than this, so a short registry still has room to fly.</param>
/// <param name="GroundMargin">How far the ground extends beyond the ring, for exhibits that run outwards.</param>
/// <param name="PillarColor">The colour of the standard pillars.</param>
public sealed record GalleryOptions(
    float Spacing = 13f,
    float MinimumRadius = 22f,
    float GroundMargin = 80f,
    Color? PillarColor = null)
{
    /// <summary>The defaults, as the shape gallery uses them.</summary>
    public static GalleryOptions Default { get; } = new();
}