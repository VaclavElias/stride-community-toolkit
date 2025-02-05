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
Entity? entity1 = null;
Entity? entity2 = null;
BodyComponent? body1 = null;
BodyComponent? body2 = null;

// Flag to indicate that the sphere is currently being dragged.
bool isDraggingSphere = false;

// The Y position at which the sphere should be dragged (i.e. stays above the ground).
const float DragYPosition = 2f;

using var game = new Game();

// Run the game loop with the Start and Update methods
game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddGroundGizmo(showAxisName: true);

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);

    entity.Transform.Position = new Vector3(0, 8, 0);

    entity.Scene = scene;

    entity1 = game.Create3DPrimitive(PrimitiveModelType.Sphere);
    entity1.Transform.Position = new Vector3(-2, 8, -2);
    body1 = entity1.Get<BodyComponent>();
    body1.Kinematic = true;

    entity2 = game.Create3DPrimitive(PrimitiveModelType.Sphere);
    entity2.Transform.Position = new Vector3(-2.1f, 16, -2.9f);
    body2 = entity2.Get<BodyComponent>();

    var constrain1 = new DistanceLimitConstraintComponent
    {
        A = body1,
        B = body2,
        MinimumDistance = 0,
        MaximumDistance = 3.0f,
    };

    entity1.Add(constrain1);

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

    entity1.Scene = scene;
    entity2.Scene = scene;

    // Retrieve the active camera from the scene
    mainCamera = scene.GetCamera();
}

// ProcessMouseDrag
void Update(Scene scene, GameTime time)
{
    if (mainCamera == null) return;

    // On mouse button press, attempt to select the sphere.
    if (game.Input.IsMouseButtonPressed(MouseButton.Left))
    {
        if (TrySelectSphere(game.Input.MousePosition))
        {
            // Stop any existing motion.
            body1.LinearVelocity = Vector3.Zero;
            body1.AngularVelocity = Vector3.Zero;

            isDraggingSphere = true;
        }
    }

    // While the mouse button is held, update the sphere's position along the ground.
    if (isDraggingSphere && game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        // Compute the intersection point of the mouse ray with the horizontal ground plane.
        Vector3 groundPosition = GetGroundIntersection(game.Input.MousePosition);

        // Update the sphere's position to follow the mouse, but fix the Y value.
        body1.Position = new Vector3(groundPosition.X, DragYPosition, groundPosition.Z);
    }

    // When the mouse button is released, stop dragging.
    if (isDraggingSphere && game.Input.IsMouseButtonReleased(MouseButton.Left))
    {
        isDraggingSphere = false;
    }
}

bool TrySelectSphere(Vector2 mousePosition)
{
    // Perform a raycast from the camera into the scene.
    bool hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && hitInfo.Collidable.Entity == entity1)
    {
        Console.WriteLine("Sphere selected for dragging.");

        return true;
    }

    return false;
}

Vector3 GetGroundIntersection(Vector2 mousePosition)
{
    // Generate a ray from the camera through the mouse position.
    // Note: Replace 'GetRayFromScreenPoint' with the appropriate method if needed.
    Ray ray = mainCamera.GetPickRay(mousePosition);

    // Define a horizontal plane at Y = 0 (ground level).
    Plane groundPlane = new Plane(Vector3.UnitY, 0);

    // Calculate intersection of the ray with the ground plane.
    if (ray.Intersects(groundPlane, out float distance))
    {
        return ray.Position + ray.Direction * distance;
    }

    // If no intersection is found, return a default vector.
    return Vector3.Zero;
}