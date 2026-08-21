using Example_CubicleCalamity.Components;
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
public class CubeSpawner(Game game, Scene scene, IReadOnlyDictionary<Color, Material> materials, int seed)
{
    private readonly Random _random = new(seed);

    /// <summary>
    /// Spawns one full <see cref="GameSettings.Rows"/> x <see cref="GameSettings.Rows"/> layer of
    /// cubes at the given height.
    /// </summary>
    /// <param name="y">Height of the layer's cube centres, in world units.</param>
    public void SpawnLayer(float y)
    {
        for (var x = 0; x < GameSettings.Rows; x++)
        {
            for (var z = 0; z < GameSettings.Rows; z++)
            {
                var cube = CreateCube();

                cube.Transform.Position = GridToWorld(x, y, z);

                AddCollider(cube);

                cube.Scene = scene;
            }
        }
    }

    /// <summary>
    /// Converts a column index into a world position.
    /// </summary>
    /// <remarks>
    /// Grid indices count from zero, so without the offset the platform would grow out of the origin
    /// in one direction only. <see cref="GameSettings.GridOrigin"/> pulls it back by half its own
    /// footprint, which centres it on the ground whatever <see cref="GameSettings.Rows"/> is.
    /// </remarks>
    private static Vector3 GridToWorld(int x, float y, int z) => new(
        x * GameSettings.CubeSize.X + GameSettings.GridOrigin,
        y * GameSettings.CubeSize.Y,
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