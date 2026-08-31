namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// What a <see cref="ChartCurve"/> needs to re-sample itself: the function and the sampling detail it was
/// created with. Held together so a view-driven chart reads them from one place.
/// </summary>
/// <param name="Function">The function being plotted.</param>
/// <param name="SampleCount">The sample count at creation; re-sampling never goes below it.</param>
/// <param name="SampleDensity">Samples per world unit, so a wider range keeps the same detail per unit.</param>
internal readonly record struct CurveSpec(Func<float, float> Function, int SampleCount, float SampleDensity);