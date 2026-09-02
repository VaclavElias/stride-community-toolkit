namespace Example15_Constraint_Rope;

/// <summary>
/// How a rope is put together. Every field here changes how stable the finished rope is.
/// </summary>
/// <param name="LinkCount">Number of segments. Longer ropes are harder to keep stable.</param>
/// <param name="LinkRadius">Radius of one segment.</param>
/// <param name="LinkSpacing">Gap left between segments, so neighbours never start out overlapping.</param>
/// <param name="LinkMass">Mass of one segment. The ratio against <paramref name="WeightMass"/> is what
/// usually decides whether a rope behaves.</param>
/// <param name="LeverArm">Where each link constraint is anchored, measured from the segment centre.
/// Zero anchors at the centre and removes angular oscillation entirely; a value near
/// <paramref name="LinkRadius"/> anchors at the segment ends, which looks more natural and is far
/// less stable.</param>
/// <param name="SkipSpan">How far ahead each segment is also tied to. 1 links only to the next
/// segment; higher values add "skip" constraints that let impulses take shortcuts along the rope.</param>
/// <param name="WeightRadius">Radius of the weight hanging on the end.</param>
/// <param name="WeightMass">Mass of that weight.</param>
public sealed record RopeSettings(
    int LinkCount,
    float LinkRadius,
    float LinkSpacing,
    float LinkMass,
    float LeverArm,
    int SkipSpan,
    float WeightRadius,
    float WeightMass);