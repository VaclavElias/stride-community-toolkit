using Stride.CommunityToolkit.Rendering;
using Stride.Core;
using Xunit;

namespace Stride.CommunityToolkit.Tests.Rendering;

/// <summary>
/// Pins the part of <see cref="DisplayScale"/> that does not need a window: the override wins over
/// detection, the floor holds, and <see cref="DisplayScale.Changed"/> fires exactly when the value
/// a consumer would read has actually moved.
/// </summary>
public class DisplayScaleTests
{
    private static DisplayScale Create() => new(new ServiceRegistry());

    [Fact]
    public void Value_DefaultsToOne_BeforeAnyWindowExists()
    {
        var scale = Create();

        Assert.Equal(1f, scale.Value);
        Assert.Equal(1f, scale.Detected);
        Assert.Null(scale.Override);
    }

    [Fact]
    public void Override_ReplacesDetection_AndRaisesChanged()
    {
        var scale = Create();
        var raised = 0;

        scale.Changed += (_, _) => raised++;

        scale.Override = 1.5f;

        Assert.Equal(1.5f, scale.Value);
        Assert.Equal(1f, scale.Detected);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Override_BelowTheFloor_IsClamped()
    {
        var scale = Create();

        scale.Override = 0f;

        Assert.Equal(DisplayScale.MinScale, scale.Value);
    }

    [Fact]
    public void ClearingOverride_ReturnsToDetected_AndRaisesChanged()
    {
        var scale = Create();
        var raised = 0;

        scale.Override = 2f;
        scale.Changed += (_, _) => raised++;

        scale.Override = null;

        Assert.Equal(1f, scale.Value);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SettingTheSameOverride_DoesNotRaiseChanged()
    {
        var scale = Create();
        var raised = 0;

        scale.Override = 2f;
        scale.Changed += (_, _) => raised++;

        scale.Override = 2f;

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Refresh_WithoutAGame_IsHarmless()
    {
        var scale = Create();

        scale.Refresh();

        Assert.Equal(1f, scale.Value);
    }
}