using Example_CubicleCalamity.Shared;
using Stride.Core.Mathematics;

namespace Example_CubicleCalamity.Gameplay;

/// <summary>
/// One level's board: its number and its dimensions, with the measurements that follow from them.
/// </summary>
/// <param name="Number">The level, counting from 1.</param>
/// <param name="Rows">Cubes along each horizontal side.</param>
/// <param name="Layers">How many layers the platform grows to.</param>
public readonly record struct LevelDefinition(int Number, int Rows, int Layers)
{
    /// <summary>
    /// Offset applied to the X and Z of every cube so the platform centres on the ground's origin
    /// rather than growing out of one corner, whatever <see cref="Rows"/> is.
    /// </summary>
    /// <remarks>
    /// Cube centres sit at <c>GridOrigin + i * CubeSize</c> for <c>i</c> in <c>[0, Rows)</c>, so the
    /// footprint spans <c>(Rows - 1) * CubeSize</c> and pulling it back by half of that puts its
    /// middle on zero.
    /// </remarks>
    public float GridOrigin => -(Rows - 1) * GameSettings.CubeSize.X * 0.5f;

    /// <summary>
    /// Middle of the finished platform, which is what the camera orbits and the game-over letters
    /// face away from.
    /// </summary>
    public Vector3 PlatformCentre => new(0, Layers * GameSettings.CubeSize.Y * 0.5f, 0);
}