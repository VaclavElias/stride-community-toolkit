using Stride.Core.Mathematics;

namespace CubeCollapse.Shared;

/// <summary>
/// A named set of cube colours.
/// </summary>
/// <param name="Name">Shown in the palette dropdown. Printable ASCII only - the debug text renderer
/// silently blanks anything else.</param>
/// <param name="Colours">The cube colours, in a fixed order.</param>
public sealed record ColourPalette(string Name, IReadOnlyList<Color> Colours);