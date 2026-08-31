using Box2D.NET;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2Worlds;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// Engine-agnostic 2D physics world wrapper around Box2D.NET: owns the native world and advances it
/// on a fixed timestep. It deliberately avoids any dependency on Stride <c>Entity</c> types; pairing
/// bodies with entities is <see cref="Box2DStrideBridge"/>'s job.
/// </summary>
public sealed class PhysicsWorld2D : IDisposable
{
    private B2WorldId _worldId;
    private PhysicsStepSettings _settings;

    private double _accumulator;

    /// <summary>Target simulation frequency in hertz used to derive the fixed step size.</summary>
    public int TargetHz
    {
        get => _settings.TargetHz;
        set => _settings = _settings with { TargetHz = value };
    }

    /// <summary>Maximum number of fixed steps processed per frame, to avoid the spiral of death.</summary>
    public int MaxStepsPerFrame
    {
        get => _settings.MaxStepsPerFrame;
        set => _settings = _settings with { MaxStepsPerFrame = value };
    }

    /// <summary>Box2D sub-step count passed to each world step.</summary>
    public int SubStepCount
    {
        get => _settings.SubStepCount;
        set => _settings = _settings with { SubStepCount = value };
    }

    /// <summary>Multiplier applied to incoming delta time before fixed-step accumulation.</summary>
    public float TimeScale
    {
        get => _settings.TimeScale;
        set => _settings = _settings with { TimeScale = value };
    }

    /// <summary>
    /// Creates a new physics world with default gravity (0, -10).
    /// </summary>
    /// <param name="settings">Optional stepping configuration; defaults to <see cref="PhysicsStepSettings"/> defaults.</param>
    public PhysicsWorld2D(PhysicsStepSettings? settings = null)
    {
        _settings = settings ?? new PhysicsStepSettings();
        var def = b2DefaultWorldDef();
        def.gravity = new B2Vec2(0f, -10f);
        _worldId = b2CreateWorld(in def);
    }

    /// <summary>
    /// Gets the native world id.
    /// </summary>
    public B2WorldId WorldId => _worldId;

    /// <summary>
    /// Adjusts global gravity.
    /// </summary>
    /// <param name="x">Gravity along the X axis.</param>
    /// <param name="y">Gravity along the Y axis (negative pulls down).</param>
    public void SetGravity(float x, float y) => b2World_SetGravity(_worldId, new B2Vec2(x, y));

    /// <summary>
    /// Advances the simulation using a fixed-timestep accumulator strategy.
    /// </summary>
    /// <param name="deltaSeconds">Elapsed real time in seconds since the last call.</param>
    /// <param name="perFixedStep">Optional callback invoked after each fixed step with the step duration.</param>
    /// <returns>The number of fixed steps performed this call.</returns>
    public int Step(float deltaSeconds, Action<float>? perFixedStep = null)
    {
        if (deltaSeconds <= 0f) return 0;

        var scaled = deltaSeconds * _settings.TimeScale;
        _accumulator += scaled;

        var fixedStep = _settings.FixedDeltaSeconds;
        int performed = 0;

        while (_accumulator >= fixedStep && performed < _settings.MaxStepsPerFrame)
        {
            b2World_Step(_worldId, fixedStep, _settings.SubStepCount);
            _accumulator -= fixedStep;
            performed++;
            perFixedStep?.Invoke(fixedStep);
        }

        return performed;
    }

    /// <summary>
    /// Disposes the underlying Box2D world.
    /// </summary>
    public void Dispose()
    {
        if (_worldId.index1 != 0)
        {
            b2DestroyWorld(_worldId);

            _worldId = default;
        }
    }
}