using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

// Constant vertical speed (units per second) for smooth vertical adjustments.
const float VerticalSpeed = 4.0f;
DebugTextPrinter? instructions = null;

// Game entities and components
CameraComponent? mainCamera = null;
Entity? draggableSphere = null;
Entity? connectedSphere = null;
Entity? referenceCapsule = null;
BodyComponent? draggableBody = null;
BodyComponent? connectedBody = null;

Entity? foundationBlock = null;
Entity? platform = null;

BodyComponent? foundationBody = null;
BodyComponent? platformBody = null;

List<Entity?> entities = [];
List<BodyComponent?> bodies = [];

// The fixed Y level for horizontal dragging (captured at drag start)
float initialDragY = 0;

// The additional vertical offset applied via key presses (starts at 0)
float verticalOffset = 0;

// The offset between the sphere's center and the initial click point to avoid recentering
Vector3 dragOffset = Vector3.Zero;

// Last known valid sphere position (used as a fallback)
Vector3 lastSpherePosition = Vector3.Zero;

// Flag to indicate that the sphere is currently being dragged
bool isDraggingSphere = false;

// Initialize the game instance
using var game = new Game();

// Run the game loop with the Start and Update methods
game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    // Set up a basic 3D scene with skybox, profiler, and a ground gizmo
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();
    game.AddGroundGizmo(new(-5, 0, -5), showAxisName: true);

    InitializeDebugTextPrinter();
    InitializeEntities(scene);

    // Retrieve the active camera from the scene
    mainCamera = scene.GetCamera();
}

void Update(Scene scene, GameTime time)
{
    if (mainCamera == null || draggableBody is null) return;

    if (game.Input.IsKeyPressed(Keys.R))
    {
        ResetTheScene(scene);
    }

    // Display on-screen instructions for the user
    DisplayInstructions();

    // On mouse button press, attempt to select the sphere
    if (game.Input.IsMouseButtonPressed(MouseButton.Left))
    {
        ProcessMouseClick();
    }

    // While the mouse button is held down, update the sphere's position
    if (isDraggingSphere && game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        // Get the horizontal (XZ) intersection point using the fixed initialDragY
        var horizontalPos = GetNewPosition(game.Input.MousePosition);

        // Add the stored drag offset to maintain the initial click offset.
        var newPosition = horizontalPos + dragOffset;

        // Adjust the vertical (Y-axis) position smoothly based on delta time and key presses.
        if (game.Input.IsKeyDown(Keys.Z))
        {
            verticalOffset += VerticalSpeed * (float)time.Elapsed.TotalSeconds;
        }

        if (game.Input.IsKeyDown(Keys.X))
        {
            verticalOffset -= VerticalSpeed * (float)time.Elapsed.TotalSeconds;
        }

        // The final Y position is the initial drag level plus the vertical offset.
        float finalY = initialDragY + verticalOffset;

        // Update the sphere's position while locking the Y coordinate
        draggableBody.Position = new Vector3(newPosition.X, finalY, newPosition.Z);

        lastSpherePosition = draggableBody.Position;
    }

    // When the mouse button is released, stop dragging
    if (isDraggingSphere && game.Input.IsMouseButtonReleased(MouseButton.Left))
    {
        isDraggingSphere = false;

        // Set the sphere back to non-kinematic so physics can resume
        draggableBody.Kinematic = false;

        // Wake the body to ensure physics updates
        draggableBody.Awake = true;
    }
}

void InitializeEntities(Scene scene)
{
    InitializeDistanceLimintConstraintExamples(scene);
    InitializeBallSocketConstraintExample(scene);
    InitializePointOnLineServoConstraintExample(scene);
}

void InitializeDistanceLimintConstraintExamples(Scene scene)
{
    // Create an additional capsule for visual reference
    referenceCapsule = game.Create3DPrimitive(PrimitiveModelType.Capsule, new() { EntityName = "Capsule" });
    referenceCapsule.Transform.Position = new Vector3(0, 3, 0);
    referenceCapsule.Scene = scene;

    // Create the draggable sphere with a golden material
    // Initially, the sphere is not kinematic. It will become kinematic while dragging
    draggableSphere = game.Create3DPrimitive(PrimitiveModelType.Sphere, new()
    {
        EntityName = "Draggable Sphere",
        Material = game.CreateMaterial(Color.Gold)
    });
    draggableSphere.Transform.Position = new Vector3(-2, 4, -2);
    draggableBody = draggableSphere.Get<BodyComponent>();

    // Create a second sphere to demonstrate a connected constraint
    connectedSphere = game.Create3DPrimitive(PrimitiveModelType.Sphere, new() { EntityName = "Connected Sphere" });
    connectedSphere.Transform.Position = new Vector3(-2.1f, 3, -2.9f);
    connectedBody = connectedSphere.Get<BodyComponent>();

    // Set up a distance limit constraint between the draggable and connected spheres
    var distanceLimit = new DistanceLimitConstraintComponent
    {
        A = draggableBody,
        B = connectedBody,
        MinimumDistance = 1,
        MaximumDistance = 3.0f
    };

    var distanceServo = new DistanceServoConstraintComponent
    {
        A = draggableBody,
        B = connectedBody,
        TargetDistance = 3.0f,
        SpringDampingRatio = 2,
        //SpringFrequency = 1,
    };

    draggableSphere.Add(distanceServo);

    // Add both entities to the scene
    draggableSphere.Scene = scene;
    connectedSphere.Scene = scene;

    entities.AddRange([referenceCapsule, draggableSphere, connectedSphere]);
    bodies.AddRange([draggableBody, connectedBody]);
}

void InitializeBallSocketConstraintExample(Scene scene)
{
    const float FoundationHeight = 3;
    const float FoundationWidth = 0.3f;
    const float PlatformHeight = 0.2f;
    const float PlatformWidth = 3;
    var exampleOffset = new Vector3(4, 0, -4);

    foundationBlock = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = "Foundation Block",
        Size = new(FoundationWidth, FoundationHeight, FoundationWidth),
        Material = game.CreateMaterial(Color.Beige),
    });
    foundationBlock.Transform.Position = new Vector3(0, FoundationHeight / 2, 0) + exampleOffset;
    foundationBody = foundationBlock.Get<BodyComponent>();
    foundationBody.Kinematic = true;

    platform = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = "Platform",
        Size = new(PlatformWidth, PlatformHeight, PlatformWidth),
        Material = game.CreateMaterial(Color.Bisque),
    });
    platform.Transform.Position = new Vector3(0, FoundationHeight + PlatformHeight, 0) + exampleOffset;
    platformBody = platform.Get<BodyComponent>();

    var ballSocket = new BallSocketConstraintComponent
    {
        A = foundationBody,
        B = platformBody,
        // Adjusting socket to be at the top of the foundation
        LocalOffsetA = new Vector3(0, 1.6f, 0),
        // Adjusting socket to be at the bottom of the platform
        LocalOffsetB = new Vector3(0, -0.1f, 0),
    };

    var ballSocket2 = new BallSocketMotorConstraintComponent
    {
        A = foundationBody,
        B = platformBody,
        LocalOffsetB = new Vector3(0, -0.1f, 0),
        TargetVelocityLocalA = new Vector3(0, -100, 0),
    };

    foundationBlock.Add(ballSocket);
    //foundationBlock.Add(ballSocket2);

    foundationBlock.Scene = scene;
    platform.Scene = scene;

    entities.AddRange([foundationBlock, platform]);
    bodies.AddRange([foundationBody, platformBody]);
}

void InitializePointOnLineServoConstraintExample(Scene scene)
{

}

// Processes the initial mouse click and selects the draggable sphere
void ProcessMouseClick()
{
    if (draggableBody is null || !TrySelectSphere(game.Input.MousePosition)) return;

    // Set the sphere to be kinematic while dragging
    draggableBody.Kinematic = true;

    isDraggingSphere = true;

    // Capture the current Y level to use for horizontal dragging
    initialDragY = draggableBody.Position.Y;

    // Reset the vertical offset
    verticalOffset = 0;
}

// Attempts to select the sphere by performing a raycast from the mouse position
// If successful, calculates the offset between the sphere's center and the click point
bool TrySelectSphere(Vector2 mousePosition)
{
    // Perform a raycast from the camera into the scene
    var hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && hitInfo.Collidable.Entity == draggableSphere)
    {
        Console.WriteLine($"Sphere selected for dragging: {hitInfo.Collidable.Entity.Transform.Position}");

        // Calculate the offset between the sphere's center and the hit point
        dragOffset = draggableBody!.Position - hitInfo.Point;

        return true;
    }

    return false;
}

// Computes the intersection point between the camera's pick ray and a horizontal plane at dragYPosition
// This is used to update the sphere's new position based on mouse movement
Vector3 GetNewPosition(Vector2 mousePosition)
{
    // Create a pick ray from the camera through the given mouse position
    var ray = mainCamera!.GetPickRay(mousePosition);

    // Define a horizontal plane at Y = dragYPosition.
    // For a plane defined by Normal and D, D must be -dragYPosition
    var horizontalPlane = new Plane(Vector3.UnitY, -initialDragY);

    if (ray.Intersects(in horizontalPlane, out float distance))
    {
        return ray.Position + ray.Direction * distance;
    }

    // If no intersection is found, return the last known sphere position
    return lastSpherePosition;
}

// Resets the scene by removing all entities and reinitializing them
void ResetTheScene(Scene scene)
{
    for (int i = 0; i < entities.Count; i++)
    {
        if (entities[i] is null) continue;

        entities[i]!.Scene = null;
        entities[i] = null;
    }

    for (int i = 0; i < bodies.Count; i++)
    {
        bodies[i] = null;
    }

    InitializeEntities(scene);
}

void DisplayInstructions()
{
    instructions?.Print();
}

void InitializeDebugTextPrinter()
{
    var screenSize = new Int2(game.GraphicsDevice.Presenter.BackBuffer.Width, game.GraphicsDevice.Presenter.BackBuffer.Height);

    instructions = new DebugTextPrinter()
    {
        DebugTextSystem = game.DebugTextSystem,
        TextSize = new(205, 17 * 4),
        ScreenSize = screenSize,
        Instructions = [
            new("GAME INSTRUCTIONS"),
            new("Click the golden sphere and drag to move it (Y-axis locked)"),
            new("Hold Z to move up, X to move down the golded sphere", Color.Yellow),
            new("Press R to reset the scene", Color.Yellow),
        ]
    };

    instructions.Initialize(DisplayPosition.BottomLeft);
}