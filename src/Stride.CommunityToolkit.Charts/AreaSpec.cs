namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The definition of a shaded region: the two functions bounding it, the stretch of <c>x</c> it covers and
/// how finely it is sampled. Held together so a re-sample after a view change reads from one place.
/// </summary>
/// <param name="Upper">The function bounding the region above.</param>
/// <param name="Lower">The function bounding the region below.</param>
/// <param name="From">The first <c>x</c> of the stretch, before any trimming to the chart's range.</param>
/// <param name="To">The last <c>x</c> of the stretch, before any trimming to the chart's range.</param>
/// <param name="Samples">How many points each bound is sampled at across the visible stretch.</param>
internal readonly record struct AreaSpec(
    Func<float, float> Upper,
    Func<float, float> Lower,
    float From,
    float To,
    int Samples);