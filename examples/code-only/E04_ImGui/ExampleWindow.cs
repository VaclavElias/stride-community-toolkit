using Hexa.NET.ImGui;
using Stride.CommunityToolkit.ImGui;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace E04_ImGui;

/// <summary>
/// A window of this example's own: a menu bar, a slider that drives the cube's spin, a checkbox and live stats. Nothing is
/// synced, because every frame describes the window again from whatever the values are.
/// </summary>
public sealed class ExampleWindow(IServiceRegistry services, Game game, Entity cube) : BaseWindow(services)
{
    private const float DefaultSpin = 0.25f;

    private bool _showStats = true;

    /// <summary>Turns per second the cube makes, set from the slider.</summary>
    public float SpinSpeed { get; private set; } = DefaultSpin;

    protected override ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.MenuBar;

    // Opened beside the debug tools, which take the top-left corner
    protected override System.Numerics.Vector2? WindowPos => new System.Numerics.Vector2(460f, 20f);

    protected override System.Numerics.Vector2? WindowSize => new System.Numerics.Vector2(340f, 170f);

    protected override void OnDraw(bool collapsed)
    {
        if (collapsed) return;

        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("Cube"))
            {
                if (ImGui.MenuItem("Stop")) SpinSpeed = 0f;

                if (ImGui.MenuItem("Reset"))
                {
                    SpinSpeed = DefaultSpin;
                    cube.Transform.Rotation = Quaternion.Identity;
                }

                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        var spin = SpinSpeed;

        if (ImGui.SliderFloat("Spin, turns per second", ref spin, 0f, 2f)) SpinSpeed = spin;

        ImGui.Checkbox("Show stats", ref _showStats);

        if (!_showStats) return;

        ImGui.Separator();
        ImGui.Text($"{game.UpdateTime.FramePerSecond:0} fps");
        ImGui.Text($"Cube yaw {MathUtil.RadiansToDegrees(cube.Transform.RotationEulerXYZ.Y):0} degrees");
        ImGui.Text($"{game.SceneSystem.SceneInstance.RootScene.Entities.Count} entities in the scene");
    }

    protected override void OnDestroy()
    {
    }
}