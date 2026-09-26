using Stride.Core.Diagnostics;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;

namespace Stride.CommunityToolkit.Rendering.Text;

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
    /// Light rather than dark, because <see cref="Renderers.EntityDebugSceneRendererOptions.FontColor"/>
    /// defaults to black. The two renderers previously shared a single default, which meant a change
    /// made to suit one of them silently ruined the other: giving the shared value a dark colour to
    /// suit white text turned debug labels into black-on-black. A default background only makes sense
    /// paired with a default text colour, so each renderer now owns its own pair.
    /// </remarks>
    public static readonly Color4 DefaultDebugBackground = new(0.85f, 0.85f, 0.85f, 0.6f);

    private static readonly Logger Log = GlobalLogger.GetLogger(nameof(RendererDefaults));

    /// <summary>
    /// The font a text renderer falls back on: Stride's built-in one where the content database can
    /// serve it, which is a running game, and a system sans-serif where it cannot. Game Studio's
    /// scene editor is the second case - its database holds the project's built assets, not the
    /// engine's, and its not-found path throws rather than returns - so this is what lets a text
    /// component draw in the viewport. Null only when there is no system font either.
    /// </summary>
    /// <param name="content">The renderer's content manager.</param>
    /// <param name="services">The renderer's services, for the font system.</param>
    /// <param name="size">Size of the system font, in pixels; every draw rasterises at its own size anyway.</param>
    internal static SpriteFont? LoadDefaultFont(IContentManager content, IServiceRegistry services, float size)
    {
        if (content.Exists(DefaultFontPath))
        {
            return content.Load<SpriteFont>(DefaultFontPath);
        }

        Log.Info("The built-in font is not in the content database, as in Game Studio's scene editor; using a system font.");

        return SystemFonts.LoadFirst(services, SystemFonts.SansSerifCandidates, size);
    }
}