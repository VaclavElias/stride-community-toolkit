using Stride.CommunityToolkit.Charts;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Charts;

/// <summary>
/// Pins <see cref="Chart.NiceTickStep"/>: the 1-2-5 series step a view-driven chart uses so grid lines land
/// on readable values at every zoom level.
/// </summary>
public class NiceTickStepTests
{
    private const float Tolerance = 1e-5f;

    [Theory]
    [InlineData(10f, 1f)]
    [InlineData(7f, 1f)]
    [InlineData(20f, 2f)]
    [InlineData(50f, 5f)]
    [InlineData(100f, 10f)]
    [InlineData(1000f, 100f)]
    [InlineData(0.7f, 0.1f)]
    [InlineData(0.09f, 0.01f)]
    public void ReturnsTheExpectedStep(float range, float expected)
    {
        Assert.Equal(expected, Chart.NiceTickStep(range), Tolerance);
    }

    [Fact]
    public void NeverProducesMoreLinesThanTheTarget()
    {
        // Sweep four decades of zoom; the invariant is what the chart relies on
        for (var range = 0.05f; range < 500f; range *= 1.37f)
        {
            var step = Chart.NiceTickStep(range, targetLines: 10);

            Assert.True(range / step <= 10f + Tolerance, $"range {range} gave step {step} = {range / step} lines");
        }
    }

    [Fact]
    public void StepIsAlwaysFromThe125Series()
    {
        for (var range = 0.05f; range < 500f; range *= 1.37f)
        {
            var step = Chart.NiceTickStep(range);
            var magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(step)));
            var mantissa = step / magnitude;

            Assert.True(MathF.Abs(mantissa - 1f) < 1e-3f || MathF.Abs(mantissa - 2f) < 1e-3f || MathF.Abs(mantissa - 5f) < 1e-3f,
                $"range {range} gave step {step} with mantissa {mantissa}");
        }
    }

    [Fact]
    public void RejectsNonPositiveRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Chart.NiceTickStep(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Chart.NiceTickStep(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Chart.NiceTickStep(float.NaN));
    }
}
