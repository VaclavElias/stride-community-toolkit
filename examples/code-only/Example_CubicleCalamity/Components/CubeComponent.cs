using Stride.Core.Mathematics;
using Stride.Engine;

namespace Example_CubicleCalamity.Components;

/// <summary>
/// Marks an entity as a playable cube and remembers the colour it was given.
/// </summary>
/// <remarks>
/// The colour is stored here rather than read back from the material, because the material is shared
/// between every cube of that colour and comparing materials would say nothing about what the player
/// sees. This component is what makes a cube a cube: the raycast finds an entity, and its presence is
/// what says the entity is in play.
/// </remarks>
public class CubeComponent : EntityComponent
{
    /// <summary>
    /// Gets or sets the cube's colour. Cubes match when their colours are equal.
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// Gets or sets which column and layer the cube occupies.
    /// </summary>
    /// <remarks>
    /// Maintained by <see cref="Gameplay.CubeGrid"/>, and updated the moment a clear happens rather
    /// than when the cube finishes falling. Matching reads this instead of the transform, so a click
    /// during a collapse still finds the right neighbours.
    /// </remarks>
    public Int3 GridPosition { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CubeComponent"/> class.
    /// </summary>
    /// <remarks>
    /// Stride requires a public parameterless constructor on every component so it can create one
    /// while deserializing a scene. Without it the STRDIAG010 analyser warns, even in a code-only
    /// project that never loads a scene from disk.
    /// </remarks>
    public CubeComponent() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CubeComponent"/> class with a colour.
    /// </summary>
    /// <param name="color">The colour this cube is painted.</param>
    public CubeComponent(Color color) => Color = color;
}