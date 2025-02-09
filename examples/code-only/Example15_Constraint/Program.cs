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

// Flag to indicate that the sphere is currently being dragged.
bool isDraggingSphere = false;

// The Y position at which the sphere should be dragged (i.e. stays above the ground).
//const float DragYPosition = 2f;

using var game = new Game();

// Run the game loop with the Start and Update methods
game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    // Setup a basic 3D scene with a skybox and a ground gizmo.
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddGroundGizmo(showAxisName: true);

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule, new() { EntityName = "Capsule" });
    entity.Transform.Position = new Vector3(0, 3, 0);
    entity.Scene = scene;

    draggableSphere = game.Create3DPrimitive(PrimitiveModelType.Sphere, new() { EntityName = "Draggable Sphere" });
    draggableSphere.Transform.Position = new Vector3(-2, 4, -2);
    draggableBody = draggableSphere.Get<BodyComponent>();
    draggableBody.Kinematic = true;

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

    //var constrain2 = new SwingLimitConstraintComponent
    //{
    //    A = body1,
    //    B = body1,
    //    AxisLocalA = Vector3.UnitZ,
    //    AxisLocalB = Vector3.UnitZ,
    //    SpringDampingRatio = 1,
    //    SpringFrequency = 120,
    //    MaximumSwingAngle = MathF.PI * 0.05f,
    //};

    //entity1.Add(constrain2);

    draggableSphere.Scene = scene;
    connectedSphere.Scene = scene;

    // Retrieve the active camera from the scene
    mainCamera = scene.GetCamera();
}

// ProcessMouseDrag
void Update(Scene scene, GameTime time)
{
    if (mainCamera == null) return;

    // Display on-screen instructions for the user
    DisplayInstructions(game);

    // On mouse button press, attempt to select the sphere.
    if (game.Input.IsMouseButtonPressed(MouseButton.Left))
    {
        if (TrySelectSphere(game.Input.MousePosition))
        {
            // Stop any existing motion.
            draggableBody.LinearVelocity = Vector3.Zero;
            draggableBody.AngularVelocity = Vector3.Zero;

            isDraggingSphere = true;
            dragYPosition = draggableBody.Position.Y;
        }
    }

    // While the mouse button is held, update the sphere's position along the ground.
    if (isDraggingSphere && game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        var newPosition = GetNewPosition(game.Input.MousePosition) + dragOffset;

        // Update the sphere's position to follow the mouse, but fix the Y value.
        draggableBody.Position = new Vector3(newPosition.X, dragYPosition, newPosition.Z);

        lastSpherePosition = draggableBody.Position;
    }

    // When the mouse button is released, stop dragging.
    if (isDraggingSphere && game.Input.IsMouseButtonReleased(MouseButton.Left))
    {
        isDraggingSphere = false;

        draggableBody.Awake = true;
    }
}

bool TrySelectSphere(Vector2 mousePosition)
{
    // Perform a raycast from the camera into the scene.
    var hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && hitInfo.Collidable.Entity == draggableSphere)
    {
        Console.WriteLine($"Sphere selected for dragging: {hitInfo.Collidable.Entity.Transform.Position}");

        //var clickIntersection = GetNewPosition(mousePosition);

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
    game.DebugTextSystem.Print("Click the sphere and hold to move it around", new(5, 50));
}
//game.DebugTextSystem.Print("Hold a key Y to move vertically", new(5, 30));