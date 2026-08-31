namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Configuration for fixed-timestep simulation stepping used by <see cref="PhysicsWorld2D"/>.
/// </summary>
/// <param name="TargetHz">Target simulation frequency in hertz; the fixed step size is derived from it.</param>
/// <param name="MaxStepsPerFrame">Maximum number of fixed steps processed per frame, to avoid the spiral of death.</param>
/// <param name="SubStepCount">Box2D sub-step count passed to each world step.</param>
/// <param name="TimeScale">Multiplier applied to incoming delta time before fixed-step accumulation.</param>
public sealed record PhysicsStepSettings(
    int TargetHz = 60,
    int MaxStepsPerFrame = 3,
    int SubStepCount = 4,
    float TimeScale = 1f)
{
    /// <summary>
    /// Duration of one fixed step in seconds (derived from <see cref="TargetHz"/>).
    /// </summary>
    public float FixedDeltaSeconds => 1f / TargetHz;
}