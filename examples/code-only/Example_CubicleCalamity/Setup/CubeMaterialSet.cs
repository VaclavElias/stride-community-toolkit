using Stride.Core.Mathematics;
using Stride.Rendering;

namespace Example_CubicleCalamity.Setup;

/// <summary>
/// The cube materials in all three hover states, each keyed by the cube's base colour.
/// </summary>
/// <param name="Normal">What every cube wears when the mouse is elsewhere.</param>
/// <param name="Brightened">Worn by every member of a clearable group under the mouse.</param>
/// <param name="Dimmed">Worn by a hovered lone cube, to say "this one is dead" without a click.</param>
public sealed record CubeMaterialSet(
    IReadOnlyDictionary<Color, Material> Normal,
    IReadOnlyDictionary<Color, Material> Brightened,
    IReadOnlyDictionary<Color, Material> Dimmed);