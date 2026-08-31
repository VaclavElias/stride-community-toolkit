namespace Stride.CommunityToolkit.Renderers;

/// <summary>
/// Shared default values used across the built-in scene renderers.
/// </summary>
internal static class RendererDefaults
{
    /// <summary>
    /// Content path of the default font used for on-screen debug/overlay text.
    /// </summary>
    public static readonly string DefaultFontPath = "/Stride.Engine/StrideDefaultFont";

    /// <summary>
    /// Default background color used behind on-screen debug/overlay text.
    /// </summary>
    /// <remarks>
    /// A dark, half-transparent panel, to sit behind the light default text of
    /// <see cref="EntityTextRenderer"/>. This used to be a near-white with an alpha of 0.01,
    /// multiplied into a backing texture that was itself near-transparent - the product was invisible,
    /// so no background ever actually appeared, including where a caller explicitly asked for one.
    /// </remarks>
    public static readonly Color4 DefaultBackground = new(0f, 0f, 0f, 0.5f);

    /// <summary>
    /// Default background color used behind entity debug text.
    /// </summary>
    /// <remarks>
    /// Light rather than dark, because <see cref="EntityDebugSceneRendererOptions.FontColor"/>
    /// defaults to black. The two renderers previously shared a single default, which meant a change
    /// made to suit one of them silently ruined the other: giving the shared value a dark colour to
    /// suit white text turned debug labels into black-on-black. A default background only makes sense
    /// paired with a default text colour, so each renderer now owns its own pair.
    /// </remarks>
    public static readonly Color4 DefaultDebugBackground = new(0.85f, 0.85f, 0.85f, 0.6f);
}