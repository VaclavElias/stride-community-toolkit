using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.ImGuiNet;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;

using var game = new Game();

ImGuiNetSystem? imguiSystem = null;
Entity? movingCube = null;
float time = 0f;

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    // Setup the base 3D scene with default lighting, camera, etc.
    game.SetupBase3DScene();
    game.AddSkybox();

    // Initialize ImGui.NET system for text rendering (similar to Box2D.NET)
    imguiSystem = game.AddImGuiNet();

    // Create a moving cube to demonstrate world-space text
    movingCube = game.Create3DPrimitive(PrimitiveModelType.Cube);
    movingCube.Transform.Position = new Vector3(0, 2, 0);
    movingCube.Scene = scene;

    // Add physics for more interesting movement
    //movingCube.AddRigidBody(RigidBodyTypes.Dynamic);

    Stride.CommunityToolkit.Games.GameExtensions.SetMaxFPS(game, 60);
}

void Update(Scene scene, GameTime gameTime)
{
    if (imguiSystem == null || movingCube == null) return;

    time += (float)gameTime.Elapsed.TotalSeconds;

    // Move the cube in a circle
    var radius = 3f;
    movingCube.Transform.Position = new Vector3(
        MathF.Sin(time) * radius,
        2f + MathF.Sin(time * 2f) * 0.5f,
        MathF.Cos(time) * radius
    );

    // Draw text at screen coordinates (similar to Box2D.NET's DrawString method)
    imguiSystem.DrawString(10, 10, "ImGui.NET Text Rendering Example");
    imguiSystem.DrawString(10, 30, $"Frame Time: {gameTime.Elapsed.TotalMilliseconds:F2}ms");
    imguiSystem.DrawString(10, 50, "Press ESC to exit");

    // Draw text at world coordinates (following the moving cube)
    imguiSystem.DrawString(movingCube.Transform.Position + Vector3.UnitY,
        "Moving Cube", new(255, 255, 0, 0)); // Yellow text

    // Draw some colored text at fixed world positions
    imguiSystem.DrawString(new Vector3(-2, 1, 0), "Red Text", new(255, 100, 100, 0));
    imguiSystem.DrawString(new Vector3(2, 1, 0), "Green Text", new(100, 255, 100, 0));
    imguiSystem.DrawString(new Vector3(0, 1, -2), "Blue Text", new(100, 100, 255, 0));

    // Performance info (bottom of screen)
    var windowHeight = game.Window.ClientBounds.Height;
    imguiSystem.DrawString(10, windowHeight - 60,
        $"Camera Position: {game.SceneSystem.SceneInstance.RootScene.Entities.First().Transform.Position}");
    imguiSystem.DrawString(10, windowHeight - 40,
        $"Entities: {scene.Entities.Count}");
    imguiSystem.DrawString(10, windowHeight - 20,
        $"Time: {time:F1}s");
}