using Stride.BepuPhysics;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Gizmos;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

CameraComponent? camera = null;
ModelComponent? lineModelComponent = null;
Entity? entity = null;
BodyComponent? body = null;
Buffer? vertexBuffer = null;
Vector3[] vertices = new Vector3[2]; // Start and end points

using var game = new Game();

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();
    game.AddGroundGizmo(new(-5, 0, -5), showAxisName: true);

    entity = game.Create3DPrimitive(PrimitiveModelType.Sphere);
    entity.Transform.Position = new Vector3(0, 8, 0);
    body = entity.Get<BodyComponent>();

    entity.Scene = scene;

    camera = scene.GetCamera();

    var lineEntity = CreateLineEntity(game);

    entity.AddChild(lineEntity);
}

void Update(Scene scene, GameTime time)
{
    if (camera == null) return;

    game.DebugTextSystem.Print("Click the ground to apply a direction impulse", new(5, 30));
    game.DebugTextSystem.Print("Click the sphere to stop moving", new(5, 50));

    if (game.Input.IsMouseButtonPressed(MouseButton.Left))
    {
        var hit = camera.Raycast(game.Input.MousePosition, 100, out var hitInfo);

        if (hit)
        {
            Console.WriteLine($"Hit entity: {hitInfo.Collidable.Entity.Name}");

            if (hitInfo.Collidable.Entity == entity && body != null)
            {
                body.LinearVelocity = Vector3.Zero;
                body.AngularVelocity = Vector3.Zero;

                return;
            }

            // Transform the hit point from world space to local space of the sphere entity
            var localHitPoint = Vector3.Transform(hitInfo.Point, Matrix.Invert(entity.Transform.WorldMatrix));

            // Update the end vertex
            vertices[1] = localHitPoint.XYZ();

            // Re-upload vertex data to GPU
            vertexBuffer?.SetData(game.GraphicsContext.CommandList, vertices);

            //Console.WriteLine($"{hitInfo.Collidable.Entity}, direction: {randomDirection}");

            if (body is null) return;

            // Calculate direction from sphere center to hit point
            var sphereCenter = entity.Transform.WorldMatrix.TranslationVector;
            var direction = hitInfo.Point - sphereCenter;

            // Normalize the direction
            direction.Normalize();

            // Define the impulse magnitude (adjust as needed)
            var impulseMagnitude = 0.5f;

            // Calculate the impulse vector
            var impulse = direction * impulseMagnitude;

            // Calculate an offset to apply the impulse at the surface of the sphere
            var sphereRadius = 1f; // Replace with your sphere's actual radius if different
            var offset = direction * sphereRadius;

            // Apply the impulse at the offset position to induce rotation
            body.ApplyImpulse(impulse, offset);

            body.Awake = true;
        }
        else
        {
            Console.WriteLine("No hit");
        }
    }
}

Entity CreateLineEntity(Game game)
{
    // Initialize vertices (start at origin, end at origin)
    vertices[0] = Vector3.Zero;
    vertices[1] = new(-1, 1, 1);

    // Create vertex buffer with start and end points
    vertexBuffer = Buffer.New(game.GraphicsDevice, vertices, BufferFlags.VertexBuffer);

    // Create index buffer
    var indices = new ushort[] { 0, 1 };
    var indexBuffer = Buffer.New(game.GraphicsDevice, indices, BufferFlags.IndexBuffer);

    var meshDraw = new MeshDraw
    {
        PrimitiveType = PrimitiveType.LineList,
        VertexBuffers = [new VertexBufferBinding(vertexBuffer, new VertexDeclaration(VertexElement.Position<Vector3>()), vertices.Length)],
        IndexBuffer = new IndexBufferBinding(indexBuffer, is32Bit: false, indices.Length),
        DrawCount = indices.Length
    };

    var mesh = new Mesh { Draw = meshDraw };

    lineModelComponent = new ModelComponent { Model = new Model { mesh, GizmoEmissiveColorMaterial.Create(game.GraphicsDevice, Color.DarkMagenta) } };

    return new Entity { lineModelComponent };
}