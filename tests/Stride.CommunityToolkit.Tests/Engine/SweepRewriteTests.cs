using Stride.CommunityToolkit.Collections;
using Stride.CommunityToolkit.Rendering.Text;
using Stride.CommunityToolkit.Scripts;
using Stride.Input;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Engine;

/// <summary>
/// Pins the small helpers the second sweep rewrote without changing what they do: the list resize on
/// <c>CollectionsMarshal</c>, the dictionary counter, the cameras' shared zoom step and the cached key names.
/// </summary>
public class SweepRewriteTests
{
    [Fact]
    public void EnsureSize_GrowsWithDefaults_AndLeavesALongerListAlone()
    {
        var list = new List<int> { 1, 2 };

        list.EnsureSize(5);

        Assert.Equal([1, 2, 0, 0, 0], list);

        list.EnsureSize(3);

        Assert.Equal(5, list.Count);
    }

    [Fact]
    public void SetCount_GrownAfterAShrink_DoesNotBringTheOldItemsBack()
    {
        var list = new List<string?> { "a", "b", "c", "d" };

        list.SetCount(1);

        Assert.Equal(["a"], list);

        // The backing array still held b, c and d; growing must show defaults, not them
        list.SetCount(4);

        Assert.Equal(["a", null, null, null], list);
    }

    [Fact]
    public void SetCount_OfValueTypes_GrownAfterAShrink_IsZeroed()
    {
        var list = new List<int> { 7, 8, 9 };

        list.SetCount(1);
        list.SetCount(3);

        Assert.Equal([7, 0, 0], list);
    }

    [Fact]
    public void Increment_StartsAtOne_AndCountsUp()
    {
        var counts = new Dictionary<string, int>();

        Assert.Equal(1, counts.Increment("a"));
        Assert.Equal(2, counts.Increment("a"));
        Assert.Equal(1, counts.Increment("b"));
        Assert.Equal(2, counts["a"]);
    }

    [Theory]
    [InlineData(10f, 1f, 9.0909f)]   // one notch in: divided by 1.1
    [InlineData(10f, -1f, 11f)]      // one notch out: multiplied by 1.1
    [InlineData(10f, 0f, 10f)]
    [InlineData(10f, 100f, 2f)]      // clamped to the minimum
    [InlineData(10f, -100f, 50f)]    // clamped to the maximum
    public void StepZoom_IsMultiplicativePerNotch_AndClamped(float value, float wheel, float expected)
        => Assert.Equal(expected, CameraMath.StepZoom(value, wheel, step: 0.1f, min: 2f, max: 50f), 3);

    [Theory]
    [InlineData(Keys.D2, "2")]
    [InlineData(Keys.NumPad7, "7")]
    [InlineData(Keys.F3, "F3")]
    [InlineData(Keys.Space, "Space")]
    public void KeyNames_ShowDigitsBare_AndEveryOtherKeyByName(Keys key, string expected)
    {
        Assert.Equal(expected, KeyNames.Describe(key));

        // The second ask is the cached one
        Assert.Equal(expected, KeyNames.Describe(key));
    }

    [Fact]
    public void KeyNames_AskedFromManyThreadsAtOnce_AgreeAndDoNotThrow()
    {
        var keys = Enum.GetValues<Keys>();

        Parallel.For(0, 64, _ =>
        {
            foreach (var key in keys) Assert.False(string.IsNullOrEmpty(KeyNames.Describe(key)));
        });
    }
}