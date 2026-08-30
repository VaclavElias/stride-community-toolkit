namespace Stride.CommunityToolkit.Windows;

/// <summary>
/// DPI result including the raw DPI values, the derived scale factor and whether a fallback was used.
/// </summary>
/// <param name="DpiX">Horizontal DPI value (dots per inch).</param>
/// <param name="DpiY">Vertical DPI value (dots per inch).</param>
/// <param name="IsFallback">Whether the value was obtained via a fallback method (such as GDI) rather than modern monitor APIs.</param>
public readonly record struct DpiInfo(uint DpiX, uint DpiY, bool IsFallback)
{
    /// <summary>
    /// Derived scale factor based on a 96 DPI baseline. (e.g. 96 DPI -> 1.0f)
    /// </summary>
    public float Scale => DpiX / 96f;

    /// <summary>
    /// Returns a readable representation of the DPI information including scale and fallback flag.
    /// </summary>
    /// <returns>String describing the DPI values and scale.</returns>
    public override string ToString() => $"{DpiX}x{DpiY} (Scale {Scale:F2}x){(IsFallback ? " Fallback" : string.Empty)}";
}