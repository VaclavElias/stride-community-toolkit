using Stride.CommunityToolkit.Renderers;
using Stride.Core.Diagnostics;
using Stride.Core.Serialization.Contents;
using Stride.Graphics;
using Stride.Graphics.Font;

namespace Stride.CommunityToolkit.Scripts.Utilities;

/// <summary>
/// Finds, registers and caches the font <see cref="DebugOverlay"/> draws with: the overlay's explicit
/// <see cref="DebugOverlay.Font"/> if set, otherwise an installed system font matching the overlay's
/// font settings, otherwise Stride's default font. Split out of <see cref="DebugOverlay"/> so the
/// overlay owns the settings and this owns the lookup and the caching.
/// </summary>
internal sealed class DebugOverlayFontResolver : IDisposable
{
    private static readonly Logger _log = GlobalLogger.GetLogger(nameof(DebugOverlay));

    private SpriteFont? _defaultFont;
    private SpriteFont? _systemFont;
    private string? _systemFontKey;
    private bool _systemFontFailed;

    /// <summary>
    /// <see cref="DebugOverlay.Font"/> if set; otherwise an installed font - <see cref="DebugOverlay.FontName"/>, or the first of <see cref="DebugOverlay.FontFamily"/> 's candidates that is present - registered with Stride's font system from its file the first time it is needed; otherwise Stride's default font.
    /// </summary>
    internal SpriteFont Resolve(DebugOverlay overlay, IContentManager content, IServiceRegistry services)
    {
        if (overlay.Font != null) return overlay.Font;

        var key = $"{overlay.FontFamily}|{overlay.FontName}|{overlay.FontStyle}|{overlay.FontFile}";

        if (key != _systemFontKey)
        {
            _systemFont?.Dispose();
            _systemFont = null;
            _systemFontKey = key;
            _systemFontFailed = false;
        }

        if (_systemFont is null && !_systemFontFailed)
        {
            _systemFont = LoadSystemFont(overlay, services);
            _systemFontFailed = _systemFont is null;
        }

        return _systemFont ?? (_defaultFont ??= content.Load<SpriteFont>(RendererDefaults.DefaultFontPath));
    }

    /// <summary>Releases the runtime font this resolver registered, if any. The default font belongs to the content manager and is left alone.</summary>
    public void Dispose()
    {
        _systemFont?.Dispose();
        _systemFont = null;
    }

    private static SpriteFont? LoadSystemFont(DebugOverlay overlay, IServiceRegistry services)
    {
        var fontSystem = services.GetService<FontSystem>();

        if (fontSystem is null)
        {
            _log.Warning("No FontSystem service; drawing with Stride's default font.");
            return null;
        }

        var style = overlay.FontStyle;
        var families = overlay.FontName is { Length: > 0 } explicitName ? [explicitName] : FamilyCandidates(overlay.FontFamily);

        try
        {
            foreach (var family in families)
            {
                var runtimeFonts = fontSystem.RuntimeFonts;

                if (!runtimeFonts.IsRegistered(family, style))
                {
                    var path = overlay.FontFile ?? FindSystemFontFile(family, style);

                    if (path is null) continue;

                    runtimeFonts.RegisterFont(family, path, style);
                }

                return fontSystem.LoadRuntimeFont(family, overlay.FontSize, style);
            }

            _log.Warning($"None of the fonts {string.Join(", ", families)} ({style}) were found in the system font folders; drawing with Stride's default font instead.");
        }
        catch (Exception exception)
        {
            _log.Warning($"The overlay font could not be loaded; drawing with Stride's default font instead. {exception.Message}");
        }

        return null;
    }

    /// <summary>
    /// The families tried, in order, for a <see cref="DebugOverlayFontFamily"/> on the current operating system - the platform's usual screen font first, then the metric-compatible families other platforms ship, so a font is nearly always found without any being bundled.
    /// </summary>
    private static string[] FamilyCandidates(DebugOverlayFontFamily family)
    {
        var monospace = family == DebugOverlayFontFamily.Monospace;

        if (OperatingSystem.IsWindows())
        {
            return monospace
                ? ["Consolas", "Cascadia Mono", "Courier New", "DejaVu Sans Mono"]
                : ["Segoe UI", "Arial", "DejaVu Sans"];
        }

        if (OperatingSystem.IsMacOS())
        {
            return monospace
                ? ["Menlo", "Courier New", "DejaVu Sans Mono"]
                : ["Helvetica", "Arial", "DejaVu Sans"];
        }

        return monospace
            ? ["DejaVu Sans Mono", "Liberation Mono", "Courier New"]
            : ["Liberation Sans", "DejaVu Sans", "Arial"];
    }

    /// <summary>
    /// Looks for the font file of a family in the operating system's font folders, using the known file names of the common families and the usual naming conventions for the rest.
    /// </summary>
    private static string? FindSystemFontFile(string family, FontStyle style)
    {
        var directories = SystemFontDirectories().Where(Directory.Exists).ToList();

        if (directories.Count == 0) return null;

        var wanted = new HashSet<string>(FileNameCandidates(family, style), StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories)
        {
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);

                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase))
                { continue; }

                if (wanted.Contains(Path.GetFileNameWithoutExtension(file)))
                    return file;
            }
        }

        return null;
    }

    private static IEnumerable<string> SystemFontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts");
        }
        else
        {
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fonts");
        }
    }

    /// <summary>
    /// File names (without extension) of the common families per style, for the ones whose files are not named after the family: Windows' classic eight-character names and macOS' collections. Anything else is tried under the usual conventions - "Family", "Family-Bold", "FamilyBold" and so on.
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownFontFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Regular, Bold, Italic, BoldItalic
        ["Consolas"] = ["consola", "consolab", "consolai", "consolaz"],
        ["Courier New"] = ["cour", "courbd", "couri", "courbi"],
        ["Arial"] = ["arial", "arialbd", "ariali", "arialbi"],
        ["Segoe UI"] = ["segoeui", "segoeuib", "segoeuii", "segoeuiz"],
        ["Times New Roman"] = ["times", "timesbd", "timesi", "timesbi"],
        ["Verdana"] = ["verdana", "verdanab", "verdanai", "verdanaz"],
        ["Tahoma"] = ["tahoma", "tahomabd", "tahoma", "tahomabd"],
        ["Cascadia Mono"] = ["CascadiaMono", "CascadiaMono", "CascadiaMonoItalic", "CascadiaMonoItalic"],
        ["Menlo"] = ["Menlo", "Menlo", "Menlo", "Menlo"],
        ["Helvetica"] = ["Helvetica", "Helvetica", "Helvetica", "Helvetica"],
        ["DejaVu Sans"] = ["DejaVuSans", "DejaVuSans-Bold", "DejaVuSans-Oblique", "DejaVuSans-BoldOblique"],
        ["DejaVu Sans Mono"] = ["DejaVuSansMono", "DejaVuSansMono-Bold", "DejaVuSansMono-Oblique", "DejaVuSansMono-BoldOblique"],
    };

    private static IEnumerable<string> FileNameCandidates(string family, FontStyle style)
    {
        var bold = (style & FontStyle.Bold) == FontStyle.Bold;
        var italic = (style & FontStyle.Italic) == FontStyle.Italic;
        var styleIndex = (bold ? 1 : 0) + (italic ? 2 : 0);

        if (KnownFontFiles.TryGetValue(family, out var known))
            yield return known[styleIndex];

        var compact = family.Replace(" ", string.Empty);

        var suffixes = styleIndex switch
        {
            3 => ["-BoldItalic", "BoldItalic", " Bold Italic", "bi", "z"],
            1 => ["-Bold", "Bold", " Bold", "bd", "b"],
            2 => ["-Italic", "Italic", " Italic", "i"],
            _ => new[] { string.Empty, "-Regular", "Regular", " Regular" },
        };

        foreach (var suffix in suffixes)
        {
            yield return compact + suffix;
            yield return family + suffix;
        }
    }
}