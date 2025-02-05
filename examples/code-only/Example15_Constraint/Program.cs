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

bool isButtonPressed = false;
bool movementStoped = false;

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

void Update(Scene scene, GameTime time)
{
    if (mainCamera == null) return;

    if (game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        isButtonPressed = true;
    }

    if (isButtonPressed && game.Input.IsMouseButtonDown(MouseButton.Left))
    {
        // Cast a ray from the camera into the scene based on the mouse position
        var hit = mainCamera.Raycast(game.Input.MousePosition, 100, out var hitInfo);

        if (hit)
        {
            if (hitInfo.Collidable.Entity == entity1)
            {
                if (!movementStoped)
                {
                    body1.LinearVelocity = Vector3.Zero;
                    body1.AngularVelocity = Vector3.Zero;

                    movementStoped = true;
                }

                body1.Position = hitInfo.Point;

                Console.WriteLine($"Hit entity: {hitInfo.Point}");
            }
            else
            {
                Console.WriteLine($"No desired hit.");
            }
        }

    }

    if (isButtonPressed && game.Input.IsMouseButtonReleased(MouseButton.Left))
    {
        isButtonPressed = false;
        movementStoped = false;
    }
}