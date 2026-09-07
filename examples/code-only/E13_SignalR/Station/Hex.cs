using Stride.Core.Mathematics;

namespace E13_SignalR.Station;

/// <summary>
/// Turns the shared project's <c>#RRGGBB</c> strings into Stride colours. The shared project stores
/// colours as hex because the web page reads them as CSS, and this is the game's end of that bargain.
/// </summary>
public static class Hex
{
    public static Color ToColor(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        var value = Convert.ToInt32(hex.TrimStart('#'), 16);

        return new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>The same colour at a different alpha.</summary>
    public static Color WithAlpha(Color color, byte alpha) => new(color.R, color.G, color.B, alpha);
}