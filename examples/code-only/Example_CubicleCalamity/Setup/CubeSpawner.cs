using Example_CubicleCalamity.Components;
using Example_CubicleCalamity.Gameplay;
using Example_CubicleCalamity.Shared;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Games;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Example_CubicleCalamity.Setup;

/// <summary>
/// Builds the platform one layer at a time.
/// </summary>
/// <remarks>
/// Spawning is kept apart from the game loop that decides <em>when</em> to spawn, so the rule ("a new
/// layer every <see cref="GameSettings.Interval"/> seconds until there are
/// <see cref="GameSettings.MaxLayers"/>") stays readable without the entity-building detail in the
/// way.
/// </remarks>
/// <param name="game">The running game, used to create the cube primitives.</param>
/// <param name="scene">The scene cubes are added to.</param>
/// <param name="materials">One material per colour, from <see cref="MaterialFactory"/>.</param>
/// <param name="seed">Seed for the colour picker, so a run is reproducible.</param>
public class CubeSpawner(Game game, Scene scene, CubeGrid grid, IReadOnlyDictionary<Color, Material> materials, int seed)
{
    private readonly Random _random = new(seed);

    /// <summary>
    /// Spawns one full <see cref="GameSettings.Rows"/> x <see cref="GameSettings.Rows"/> layer of
    /// cubes at the given height.
    /// </summary>
    /// <param name="layer">Which layer this is, counting from zero at the ground.</param>
    public void SpawnLayer(int layer)
    {
        for (var x = 0; x < GameSettings.Rows; x++)
        {
            for (var z = 0; z < GameSettings.Rows; z++)
            {
                var cube = CreateCube();

                cube.Transform.Position = GridToWorld(x, layer, z);

                AddCollider(cube);

                grid.Add(new Int3(x, layer, z), cube);

                cube.Scene = scene;
            }
        }
    }

    /// <summary>
    /// Converts a grid coordinate into the world position of that cube's centre.
    /// </summary>
    /// <param name="x">Column index along X.</param>
    /// <param name="layer">Layer index, counting from zero at the ground.</param>
    /// <param name="z">Column index along Z.</param>
    /// <returns>The world position of the cube centre for that coordinate.</returns>
    /// <remarks>
    /// Two offsets, for two different reasons. <see cref="GameSettings.GridOrigin"/> pulls the
    /// footprint back by half its own width so the platform centres on the ground rather than growing
    /// out of one corner, whatever <see cref="GameSettings.Rows"/> is. The half cube on Y is because
    /// a coordinate names a cube's centre while layer zero sits <em>on</em> the ground, so without it
    /// the bottom layer would be buried half way into the floor.
    /// </remarks>
    public static Vector3 GridToWorld(int x, int layer, int z) => new(
        x * GameSettings.CubeSize.X + GameSettings.GridOrigin,
        (layer + 0.5f) * GameSettings.CubeSize.Y,
        z * GameSettings.CubeSize.Z + GameSettings.GridOrigin);

    private Entity CreateCube()
    {
        var colour = GameSettings.Colours[_random.Next(0, GameSettings.Colours.Count)];

        var cube = game.Create3DPrimitive(PrimitiveModelType.Cube, new Primitive3DEntityOptions()
        {
            EntityName = EntityNames.Cube,
            Material = materials[colour],
            Size = GameSettings.CubeSize
        });

        cube.Add(new CubeComponent(colour));

        return cube;
    }

    private static void AddCollider(Entity entity)
    {
        // A single BoxCollider still has to be wrapped: ColliderBase does not implement ICollider,
        // only CompoundCollider, MeshCollider and EmptyCollider do.
        var compoundCollider = new CompoundCollider();

        compoundCollider.Colliders.Add(new BoxCollider
        {
            Size = GameSettings.CubeSize,
            // Was 1e9. All cubes shared it so the mass ratios were fine, but it also scales the
            // inertia tensor and puts contact impulses nine orders of magnitude away from where
            // Bepu's absolute epsilons and sleep thresholds are tuned.
            Mass = 1,
        });

        // Kinematic until the whole tower is built, so layers hang in the air while they spawn.
        // Nothing here may touch BodyInertia or the velocities: their setters no-op until the
        // component is added to an entity below, at which point SlidingCubeComponent.AttachInner
        // takes over and locks rotation.
        entity.Add(new SlidingCubeComponent
        {
            Collider = compoundCollider,
            Kinematic = true,
        });
    }
}