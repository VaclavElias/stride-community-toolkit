namespace Stride.CommunityToolkit.Renderers;

/// <summary>
/// Shared default values used across the built-in scene renderers.
/// </summary>
internal static class RendererDefaults
{
    /// <summary>
    /// Content path of the default font used for on-screen debug/overlay text.
    /// </summary>
    public const string DefaultFontPath = "/Stride.Engine/StrideDefaultFont";

    /// <summary>
    /// Default background color used behind on-screen debug/overlay text.
    /// </summary>
    /// <remarks>
    /// A dark, half-transparent panel, so light text stays readable over whatever the scene happens
    /// to put behind it. This used to be a near-white with an alpha of 0.01, multiplied into a
    /// backing texture that was itself near-transparent - the product was invisible, so no background
    /// ever actually appeared, including where an example explicitly asked for one.
    /// </remarks>
    public static readonly Color4 DefaultBackground = new(0f, 0f, 0f, 0.5f);
}