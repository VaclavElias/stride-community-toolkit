using Stride.BepuPhysics;
using Stride.BepuPhysics.Constraints;
using Stride.BepuPhysics.Definitions;
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
const string DraggableEntityName = "Draggable Sphere";

DebugTextPrinter? instructions = null;

// Game entities and components
CameraComponent? mainCamera = null;
BodyComponent? draggableBody = null;

Entity? foundationBlock = null;
Entity? platform = null;

BodyComponent? foundationBody = null;
BodyComponent? platformBody = null;

List<Entity?> entities = [];
List<BodyComponent?> bodies = [];

var lineLayer = CollisionLayer.Layer1;
var cubeLayer = CollisionLayer.Layer2;
var groundLayer = CollisionLayer.Layer3;
var otherLayer = CollisionLayer.Layer5;

var collisionMatrix = new CollisionMatrix();
collisionMatrix.Set(lineLayer, cubeLayer, shouldCollide: false);
collisionMatrix.Set(lineLayer, groundLayer, shouldCollide: true);
collisionMatrix.Set(lineLayer, otherLayer, shouldCollide: true);
collisionMatrix.Set(groundLayer, otherLayer, shouldCollide: true);
collisionMatrix.Set(otherLayer, otherLayer, shouldCollide: true);

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

    SetupCollisionMatrix(scene);
    SetupGroundCollisionLayer(scene);

    InitializeDebugTextPrinter();
    InitializeEntities(scene);

    // Retrieve the active camera from the scene
    mainCamera = scene.GetCamera();
}

void Update(Scene scene, GameTime time)
{
    if (mainCamera == null) return;

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
        if (draggableBody == null) return;

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

        if (draggableBody == null) return;

        // Set the sphere back to non-kinematic so physics can resume
        draggableBody.Kinematic = false;

        // Wake the body to ensure physics updates
        draggableBody.Awake = true;

        draggableBody = null;
    }
}

void SetupCollisionMatrix(Scene scene)
{
    var camera = scene.GetCamera();

    var simulation = camera?.Entity.GetSimulation();

    if (simulation == null) return;

    simulation.CollisionMatrix = collisionMatrix;
}

void SetupGroundCollisionLayer(Scene scene)
{
    var groundEntity = scene.Entities.FirstOrDefault(e => e.Name == "Ground");

    if (groundEntity == null) return;

    var groundBody = groundEntity.GetComponent<StaticComponent>();

    groundBody!.CollisionLayer = groundLayer;
}

void InitializeEntities(Scene scene)
{
    // Create reference entities for visual reference
    CreateReferenceCube(scene);
    CreateReferenceCapsule(scene);

    CreateDistanceLimintConstraintExamples(scene);
    CreateDistanceServoConstraintExamples(scene);
    CreateBallSocketConstraintExample(scene);
    //InitializePointOnLineServoConstraintExample(scene);
    CreatePointOnLineServoConstraintExample2(scene);
}

void CreateReferenceCube(Scene scene)
{
    var referenceCube = CreateEntity(PrimitiveModelType.Cube, "Reference Cube", Color.Purple, new Vector3(3, 3, 3));

    var referenceCubeBody = referenceCube.Get<BodyComponent>();
    referenceCubeBody.FrictionCoefficient = 0.1f;
    referenceCubeBody.CollisionLayer = CollisionLayer.Layer5;

    var angularServoSetB = new OneBodyAngularServoConstraintComponent
    {
        TargetOrientation = Quaternion.Identity,
        A = referenceCubeBody,
        ServoMaximumForce = 1000,
        SpringDampingRatio = 10,
        SpringFrequency = 300,
    };

    referenceCube.Add(angularServoSetB);
    referenceCube.Scene = scene;
}

void CreateReferenceCapsule(Scene scene)
{
    var referenceCapsule = CreateEntity(PrimitiveModelType.Capsule, "Reference Capsule", Color.Orange, new Vector3(0, 3, 0));

    var referenceCapsuleBody = referenceCapsule.Get<BodyComponent>();
    referenceCapsuleBody.CollisionLayer = CollisionLayer.Layer5;

    referenceCapsule.Scene = scene;
}

void CreateDistanceLimintConstraintExamples(Scene scene)
{
    // Create the draggable sphere with a golden material
    // Initially, the sphere is not kinematic. It will become kinematic while dragging
    var draggableSphere = CreateEntity(PrimitiveModelType.Sphere, DraggableEntityName, Color.Gold, new Vector3(-2, 3, -2));
    var draggableBody = draggableSphere.Get<BodyComponent>();
    draggableBody.CollisionLayer = CollisionLayer.Layer5;

    // Create a second sphere to demonstrate a connected constraint
    var connectedSphere = CreateEntity(PrimitiveModelType.Sphere, "Connected Sphere", Color.Blue, new Vector3(-2.1f, 3, -2.9f));
    var connectedBody = connectedSphere.Get<BodyComponent>();
    connectedBody.CollisionLayer = CollisionLayer.Layer5;

    // Set up a distance limit constraint between the draggable and connected spheres
    var distanceLimit = new DistanceLimitConstraintComponent
    {
        A = draggableBody,
        B = connectedBody,
        MinimumDistance = 1,
        MaximumDistance = 3.0f
    };

    draggableSphere.Add(distanceLimit);

    // Add both entities to the scene
    draggableSphere.Scene = scene;
    connectedSphere.Scene = scene;

    entities.AddRange([draggableSphere, connectedSphere]);
    bodies.AddRange([draggableBody, connectedBody]);
}

void CreateDistanceServoConstraintExamples(Scene scene)
{
    // Create the draggable sphere with a golden material
    // Initially, the sphere is not kinematic. It will become kinematic while dragging
    var draggableSphere = CreateEntity(PrimitiveModelType.Sphere, DraggableEntityName, Color.Gold, new Vector3(-2, 6, -2));
    var draggableBody = draggableSphere.Get<BodyComponent>();
    draggableBody.CollisionLayer = CollisionLayer.Layer5;

    var connectedSphere = CreateEntity(PrimitiveModelType.Sphere, "Connected Sphere", Color.LightBlue, new Vector3(-2.1f, 6, -2.9f));
    var connectedBody = connectedSphere.Get<BodyComponent>();
    connectedBody.CollisionLayer = CollisionLayer.Layer5;

    // Set up a distance servo constraint between the draggable and connected spheres
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

    entities.AddRange([draggableSphere, connectedSphere]);
    bodies.AddRange([draggableBody, connectedBody]);
}

void CreateBallSocketConstraintExample(Scene scene)
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

void CreatePointOnLineServoConstraintExample2(Scene scene)
{
    // Create two separate line entities for better control of each stack
    var lineEntityA = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = "LineA",
        Size = new(0.01f, 10, 0.01f),
        Material = game.CreateMaterial(new Color(0.5f, 0.5f, 0.5f, 0.3f)),
    });
    lineEntityA.Transform.Position = new Vector3(-4, 5f, 0);
    var lineBodyA = lineEntityA.Get<BodyComponent>();
    lineBodyA.Kinematic = true;
    lineBodyA.CollisionLayer = CollisionLayer.Layer1;

    //var collidable = lineEntityA.Get<CollidableComponent>();
    //if (collidable != null)
    //{
    //    collidable.CollisionLayer = CollisionLayer.Layer1;
    //}

    lineEntityA.Scene = scene;

    //var lineEntityB = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    //{
    //    EntityName = "LineB",
    //    Size = new(0.01f, 10, 0.01f),
    //    Material = game.CreateMaterial(Color.DarkGray),
    //});
    //lineEntityB.Transform.Position = new Vector3(-4, 5f, -2);
    //var lineBodyB = lineEntityB.Get<BodyComponent>();
    //lineBodyB.Kinematic = true;
    //lineEntityB.Scene = scene;

    // Enhanced settings for better sliding
    const float CubeSpringDampingRation = 50; // Reduced from 100
    const float SpringFrequency = 20;         // Reduced from 40
    const float FrictionCoefficient = 0.1f;   // Reduced from 0.5f for smoother sliding
    const float ServoMaxForce = 500;          // Reduced from 1000 for softer constraints

    for (int i = 0; i < 10; i++)
    {
        // First stack (SetA)
        var cubeEntitySetA = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            EntityName = "CubeStackA",
            Size = new(0.9f, 0.9f, 0.9f),    // Slightly smaller cubes to prevent collision overlap
            Material = game.CreateMaterial(Color.DarkRed),
        });
        cubeEntitySetA.Transform.Position = new Vector3(-4, i * 2, 0);
        var cubeBodySetA = cubeEntitySetA.Get<BodyComponent>();
        cubeBodySetA.SpringDampingRatio = CubeSpringDampingRation;
        cubeBodySetA.SpringFrequency = SpringFrequency;
        cubeBodySetA.FrictionCoefficient = FrictionCoefficient;
        cubeBodySetA.CollisionLayer = CollisionLayer.Layer2;

        // Tighter constraint with the line to prevent X/Z drift
        var lineServoConstraintSetA = new PointOnLineServoConstraintComponent
        {
            A = lineBodyA,
            B = cubeBodySetA,
            LocalOffsetA = Vector3.Zero,     // Anchor directly on line
            LocalOffsetB = Vector3.Zero,     // Anchor at center of cube
            LocalDirection = new Vector3(0, 1, 0),
            ServoMaximumForce = ServoMaxForce,
            SpringFrequency = 15,            // Add explicit spring frequency for smoother motion
            SpringDampingRatio = 1,          // Critical damping
        };

        // Keep orientation aligned with world axes
        var angularServoSetA = new OneBodyAngularServoConstraintComponent
        {
            TargetOrientation = Quaternion.Identity,
            A = cubeBodySetA,
            ServoMaximumForce = ServoMaxForce,
            SpringDampingRatio = 5,
            SpringFrequency = 15,            // Enable frequency for more responsive rotation control
        };

        //cubeEntitySetA.Add(lineServoConstraintSetA);
        //cubeEntitySetA.Add(angularServoSetA);
        cubeEntitySetA.Scene = scene;

        // Second stack (SetB)
        var cubeEntitySetB = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            EntityName = "CubeStackB",
            Size = new(0.9f, 0.9f, 0.9f),    // Slightly smaller cubes
            Material = game.CreateMaterial(Color.DarkRed),
        });
        cubeEntitySetB.Transform.Position = new Vector3(-4, i * 2, -1);
        var cubeBodySetB = cubeEntitySetB.Get<BodyComponent>();
        cubeBodySetB.SpringDampingRatio = CubeSpringDampingRation;
        cubeBodySetB.SpringFrequency = SpringFrequency;
        cubeBodySetB.FrictionCoefficient = FrictionCoefficient;

        //// Use the second line entity for this stack
        //var lineServoConstraintSetB = new PointOnLineServoConstraintComponent
        //{
        //    A = lineBodyB,
        //    B = cubeBodySetB,
        //    LocalOffsetA = Vector3.Zero,     // Anchor directly on line
        //    LocalOffsetB = Vector3.Zero,     // Anchor at center of cube
        //    LocalDirection = new Vector3(0, 1, 0),
        //    ServoMaximumForce = ServoMaxForce,
        //    SpringFrequency = 15,
        //    SpringDampingRatio = 1,
        //};

        //var angularServoSetB = new OneBodyAngularServoConstraintComponent
        //{
        //    TargetOrientation = Quaternion.Identity,
        //    A = cubeBodySetB,
        //    ServoMaximumForce = ServoMaxForce,
        //    SpringDampingRatio = 5,
        //    SpringFrequency = 15,
        //};

        //cubeEntitySetB.Add(lineServoConstraintSetB);
        //cubeEntitySetB.Add(angularServoSetB);
        //cubeEntitySetB.Scene = scene;
    }
}

void InitializePointOnLineServoConstraintExample(Scene scene)
{
    var lineEntity = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = "Line",
        Size = new(0.01f, 10, 0.01f),
        Material = game.CreateMaterial(Color.DarkGray),
    });
    lineEntity.Transform.Position = new Vector3(-4, 5f, 0);
    var lineBody = lineEntity.Get<BodyComponent>();
    lineBody.Kinematic = true;
    lineEntity.Scene = scene;

    const float CubeSpringDampingRation = 100;
    const float SpringFrequency = 40;
    const float FrictionCoefficient = 0.5f; // Default 1

    for (int i = 0; i < 10; i++)
    {
        var cubeEntitySetA = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            EntityName = "CubeStack",
            Size = new(1, 1, 1),
            Material = game.CreateMaterial(Color.DarkRed),
        });
        cubeEntitySetA.Transform.Position = new Vector3(-4, i * 2, -1);
        var cubeBodySetA = cubeEntitySetA.Get<BodyComponent>();
        cubeBodySetA.SpringDampingRatio = CubeSpringDampingRation;
        cubeBodySetA.SpringFrequency = SpringFrequency;
        cubeBodySetA.FrictionCoefficient = FrictionCoefficient;

        var lineServoConstraintSetA = new PointOnLineServoConstraintComponent
        {
            A = lineBody,
            B = cubeBodySetA,
            LocalOffsetA = new Vector3(0, 0, -1f),
            LocalOffsetB = new Vector3(0, 0, 0),
            LocalDirection = new Vector3(0, 1, 0),
            ServoMaximumForce = 1000,

        };

        var angularServoSetA = new OneBodyAngularServoConstraintComponent
        {
            TargetOrientation = Quaternion.Identity,
            A = cubeBodySetA,
            ServoMaximumForce = 1000,
            SpringDampingRatio = 10,
            //SpringFrequency = 30,
        };

        cubeEntitySetA.Add(lineServoConstraintSetA);
        cubeEntitySetA.Add(angularServoSetA);
        cubeEntitySetA.Scene = scene;

        var cubeEntitySetB = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            EntityName = "CubeStack",
            Size = new(1, 1, 1),
            Material = game.CreateMaterial(Color.DarkRed),
        });
        cubeEntitySetB.Transform.Position = new Vector3(-4, i * 2, -2);
        var cubeBodySetB = cubeEntitySetB.Get<BodyComponent>();
        cubeBodySetB.SpringDampingRatio = CubeSpringDampingRation;
        cubeBodySetB.SpringFrequency = SpringFrequency;
        cubeBodySetB.FrictionCoefficient = FrictionCoefficient;

        var lineServoConstraintSetB = new PointOnLineServoConstraintComponent
        {
            A = lineBody,
            B = cubeBodySetB,
            LocalOffsetA = new Vector3(0, 0, -2f),
            LocalOffsetB = new Vector3(0, 0, 0),
            LocalDirection = new Vector3(0, 1, 0),
            ServoMaximumForce = 1000,
        };
        var angularServoSetB = new OneBodyAngularServoConstraintComponent
        {
            TargetOrientation = Quaternion.Identity,
            A = cubeBodySetB,
            ServoMaximumForce = 1000,
            SpringDampingRatio = 10,
            //SpringFrequency = 30,
        };

        cubeEntitySetB.Add(lineServoConstraintSetB);
        cubeEntitySetB.Add(angularServoSetB);
        cubeEntitySetB.Scene = scene;
    }
}

void ProcessMouseClick()
{
    TryRemoveCubeStack(game.Input.MousePosition);

    TrySelectSphere(game.Input.MousePosition);
}

void TryRemoveCubeStack(Vector2 mousePosition)
{
    var hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && (hitInfo.Collidable.Entity.Name == "CubeStackA" || hitInfo.Collidable.Entity.Name == "CubeStackB"))
    {
        // Get the stack name to determine which column was clicked
        string stackName = hitInfo.Collidable.Entity.Name;

        // Apply small upward force to cubes above the clicked one to ensure movement
        var clickedY = hitInfo.Collidable.Entity.Transform.Position.Y;
        var clickedZ = hitInfo.Collidable.Entity.Transform.Position.Z;

        // Remove the clicked cube
        hitInfo.Collidable.Entity.Scene = null;

        // Find and nudge cubes above this position in the same stack
        foreach (var entity in game.SceneSystem.SceneInstance.RootScene.Entities)
        {
            if (entity.Name == stackName)
            {
                var pos = entity.Transform.Position;
                if (Math.Abs(pos.Z - clickedZ) < 0.1f && pos.Y > clickedY)
                {
                    // Add a tiny impulse to get things moving
                    var body = entity.Get<BodyComponent>();
                    if (body != null)
                    {
                        body.ApplyLinearImpulse(new Vector3(0, -0.01f, 0));
                        body.Awake = true;
                    }
                }
            }
        }
    }

    //if (hit && hitInfo.Collidable.Entity.Name == "CubeStack")
    //{
    //    hitInfo.Collidable.Entity.Scene = null;
    //}
}

// Attempts to select the sphere by performing a raycast from the mouse position
// If successful, calculates the offset between the sphere's center and the click point
bool TrySelectSphere(Vector2 mousePosition)
{
    // Perform a raycast from the camera into the scene
    var hit = mainCamera.Raycast(mousePosition, 100, out var hitInfo);

    if (hit && hitInfo.Collidable.Entity.Name == DraggableEntityName)
    {
        Console.WriteLine($"Sphere selected for dragging: {hitInfo.Collidable.Entity.Transform.Position}");

        draggableBody = hitInfo.Collidable.Entity.Get<BodyComponent>();

        //if (draggableBody == null) return false;

        // Calculate the offset between the sphere's center and the hit point
        dragOffset = draggableBody!.Position - hitInfo.Point;

        // Set the sphere to be kinematic while dragging
        draggableBody.Kinematic = true;

        isDraggingSphere = true;

        // Capture the current Y level to use for horizontal dragging
        initialDragY = draggableBody.Position.Y;

        // Reset the vertical offset
        verticalOffset = 0;

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

void DisplayInstructions() => instructions?.Print();

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

Entity CreateEntity(PrimitiveModelType type, string name, Color color, Vector3 position)
{
    var entity = game.Create3DPrimitive(type, new()
    {
        EntityName = name,
        Material = game.CreateMaterial(color),
    });

    entity.Transform.Position = position;

    return entity;
}