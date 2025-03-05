using Stride.BepuPhysics;
using Stride.BepuPhysics.Definitions;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;

// This example demonstrates how to create a simple scene with two players and an enemy entity and set up collision groups to control which objects can collide with each other.
// Define collision groups to control which objects can collide with each other
// Objects within the same group can't collide with each other
var playerCollisionGroup = new CollisionGroup { Id = 1 };
var enemyCollisionGroup = new CollisionGroup { Id = 2 };

using var game = new Game();

game.Run(start: Start);

// Sets up the initial scene with players and enemies
void Start(Scene rootScene)
{
    game.SetupBase3DScene();
    game.AddSkybox();

    // Create player entities
    CreatePlayerEntity("Player1", Color.AliceBlue, new Vector3(0, 0.5f, 0), playerCollisionGroup, rootScene);
    CreatePlayerEntity("Player2", Color.MediumPurple, new Vector3(0.5f, 8, 0.5f), playerCollisionGroup, rootScene);


    // Create enemy entity
    CreateEnemyEntity(Color.Red, new Vector3(-0.1f, 12, 0.5f), enemyCollisionGroup, rootScene);
}

/// <summary>
/// Creates a player entity with specified properties
/// </summary>
void CreatePlayerEntity(string name, Color color, Vector3 position, CollisionGroup collisionGroup, Scene scene)
{
    var player = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = name,
        Material = game.CreateMaterial(color),
    });

    // Set position in the world
    player.Transform.Position = position;

    // Configure physics for the entity
    var body = player.GetComponent<BodyComponent>();
    body!.CollisionGroup = collisionGroup;

    // Add to the scene
    player.Scene = scene;
}

/// <summary>
/// Creates an enemy entity with specified properties
/// </summary>
void CreateEnemyEntity(Color color, Vector3 position, CollisionGroup collisionGroup, Scene scene)
{
    var enemy = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        EntityName = "Enemy",
        Material = game.CreateMaterial(color),
    });

    // Set position in the world
    enemy.Transform.Position = position;

    // Configure physics for the entity
    var body = enemy.GetComponent<BodyComponent>();
    body!.CollisionGroup = collisionGroup;

    // Add to the scene
    enemy.Scene = scene;
}