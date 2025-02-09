using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

// Game entities and components
CameraComponent? mainCamera = null;
Entity? draggableSphere = null;
Entity? connectedSphere = null;
BodyComponent? draggableBody = null;
BodyComponent? connectedBody = null;
float dragYPosition = 0;

Vector3 dragOffset = Vector3.Zero;
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

    // Added just for a visual reference
    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule, new() { EntityName = "Capsule" });
    entity.Transform.Position = new Vector3(0, 3, 0);
    entity.Scene = scene;

    draggableSphere = game.Create3DPrimitive(PrimitiveModelType.Sphere, new()
    {
        EntityName = "Draggable Sphere",
        Material = game.CreateMaterial(Color.Gold)
    });
    draggableSphere.Transform.Position = new Vector3(-2, 4, -2);
    draggableBody = draggableSphere.Get<BodyComponent>();
    //draggableBody.Kinematic = true;

    connectedSphere = game.Create3DPrimitive(PrimitiveModelType.Sphere, new() { EntityName = "Connected Sphere" });
    connectedSphere.Transform.Position = new Vector3(-2.1f, 3, -2.9f);
    connectedBody = connectedSphere.Get<BodyComponent>();

    var constrain1 = new DistanceLimitConstraintComponent
    {
        A = draggableBody,
        B = connectedBody,
        MinimumDistance = 0,
        MaximumDistance = 3.0f,
    };

    draggableSphere.Add(constrain1);

    draggableSphere.Scene = scene;
    connectedSphere.Scene = scene;

    // Retrieve the active camera from the scene
    mainCamera = scene.GetCamera();
}

void Update(Scene scene, GameTime time)
{
    if (mainCamera == null) return;

    // Display on-screen instructions for the user
    DisplayInstructions(game);

    // On mouse button press, attempt to select the sphere.
    if (game.Input.IsMouseButtonPressed(MouseButton.Left))
    {
        ProcessMouseClick();
    }

    // While the mouse button is held, update the sphere's position along the ground.
    if (isDraggingSphere && game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        var newPosition = GetNewPosition(game.Input.MousePosition) + dragOffset;

        if (game.Input.IsKeyDown(Keys.Z))
        {
            dragYPosition += 0.001f;
        }

        if (game.Input.IsKeyDown(Keys.X))
        {
            dragYPosition -= 0.001f;
        }

        // Update the sphere's position to follow the mouse, but fix the Y value.
        draggableBody.Position = new Vector3(newPosition.X, dragYPosition, newPosition.Z);

        lastSpherePosition = draggableBody.Position;
    }

    // When the mouse button is released, stop dragging.
    if (isDraggingSphere && game.Input.IsMouseButtonReleased(MouseButton.Left))
    {
        isDraggingSphere = false;

        draggableBody.Kinematic = false;

        draggableBody.Awake = true;
    }
}

void ProcessMouseClick()
{
    if (draggableBody is null || !TrySelectSphere(game.Input.MousePosition)) return;

    draggableBody.Kinematic = true;

    isDraggingSphere = true;

    dragYPosition = draggableBody.Position.Y;
}

bool TrySelectSphere(Vector2 mousePosition)
{
    // Perform a raycast from the camera into the scene.
    var hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && hitInfo.Collidable.Entity == draggableSphere)
    {
        Console.WriteLine($"Sphere selected for dragging: {hitInfo.Collidable.Entity.Transform.Position}");

        // Record the offset so the sphere doesn't recenter.
        dragOffset = draggableBody!.Position - hitInfo.Point;

        return true;
    }

    return false;
}

Vector3 GetNewPosition(Vector2 mousePosition)
{
    // Create a ray from the camera through the mouse position.
    var ray = mainCamera!.GetPickRay(mousePosition);
    // Define a horizontal plane at Y = dragYPosition.
    // For a plane defined by Normal and D, D must be -dragYPosition.
    var horizontalPlane = new Plane(Vector3.UnitY, -dragYPosition);

    if (ray.Intersects(horizontalPlane, out float distance))
    {
        return ray.Position + ray.Direction * distance;
    }

    // Fallback to the last known sphere position if no intersection is found.
    return lastSpherePosition;
}

static void DisplayInstructions(Game game)
{
    game.DebugTextSystem.Print("Hold a key Y to move vertically", new(5, 30));
    game.DebugTextSystem.Print("Click the golden sphere and hold to move it around", new(5, 50));
}