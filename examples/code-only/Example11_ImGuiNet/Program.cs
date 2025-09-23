using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.ImGuiNet;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using System.Runtime.InteropServices;

WindowsDpi.EnablePerMonitorV2();

DpiDiagnostics.LogDpiInfo("before Game: ");

using var game = new Game();

ImGuiNetSystem? imguiSystem = null;
Entity? movingCube = null;
float time = 0f;

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.Window.AllowUserResizing = true;
    game.Window.Title = "ImgGuiNet example";

    // Setup the base 3D scene with default lighting, camera, etc.
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();

    // Initialize ImGui.NET system for text rendering (similar to Box2D.NET)
    imguiSystem = game.AddImGuiNet();

    // Create a moving cube to demonstrate world-space text
    movingCube = game.Create3DPrimitive(PrimitiveModelType.Cube);
    movingCube.Transform.Position = new Vector3(0, 2, 0);
    movingCube.Scene = scene;

    Stride.CommunityToolkit.Games.GameExtensions.SetMaxFPS(game, 60);
}

void Update(Scene scene, GameTime gameTime)
{
    if (imguiSystem == null || movingCube == null) return;

    // first frame only if condition
    if (gameTime.FrameCount == 1)
    {
        DpiDiagnostics.LogDpiInfo("after window: ");
    }

    time += (float)gameTime.Elapsed.TotalSeconds;

    // Move the cube in a circle
    var radius = 3f;
    movingCube.Transform.Position = new Vector3(
        MathF.Sin(time) * radius,
        2f + MathF.Sin(time * 2f) * 0.5f,
        MathF.Cos(time) * radius
    );

    // Draw text at screen coordinates (similar to Box2D.NET's DrawString method)
    imguiSystem.DrawString(10, 20, "ImGui.NET Text Rendering Example");
    imguiSystem.DrawString(10, 40, $"Frame Time: {gameTime.Elapsed.TotalMilliseconds:F2}ms");
    imguiSystem.DrawString(10, 60, "Press ESC to exit");

    // Draw text at world coordinates (following the moving cube)
    imguiSystem.DrawString(movingCube.Transform.Position + Vector3.UnitY,
        "Moving Cube", new(255, 255, 0, 255)); // Yellow text

    // Draw some colored text at fixed world positions
    imguiSystem.DrawString(new Vector3(-2, 1, 0), "Red Text", new(255, 0, 0, 255));
    imguiSystem.DrawString(new Vector3(2, 1, 0), "Green Text", new(0, 255, 0, 255));
    imguiSystem.DrawString(new Vector3(0, 1, -2), "Blue Text", new(0, 0, 255, 255));

    // Performance info (bottom of screen)
    var windowHeight = game.Window.ClientBounds.Height;

    imguiSystem.DrawString(10, windowHeight - 60,
        $"Camera Position: {game.SceneSystem.SceneInstance.RootScene.Entities.First().Transform.Position}");
    imguiSystem.DrawString(10, windowHeight - 40,
        $"Entities: {scene.Entities.Count}");
    imguiSystem.DrawString(10, windowHeight - 20,
        $"Time: {time:F1}s");
}

internal static class WindowsDpi
{
    // Windows 10+ best option
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

    [DllImport("User32.dll", ExactSpelling = true, SetLastError = false)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("Shcore.dll")]
    private static extern int SetProcessDpiAwareness(int value); // 2 = PerMonitor

    public static void EnablePerMonitorV2()
    {
        // Only attempt to call Win32 APIs when running on Windows at runtime.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            // Try Per-Monitor-V2 first (Win10+)
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            return;
        }
        catch
        {
            // ignore and fallback
        }

        try
        {
            // Fallback to Per-Monitor (older Windows 8.1+)
            // 0=Unaware, 1=System, 2=PerMonitor
            SetProcessDpiAwareness(2);
        }
        catch
        {
            // ignore
        }
    }
}