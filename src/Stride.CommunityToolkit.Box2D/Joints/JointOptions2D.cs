using Stride.Core.Mathematics;

namespace Stride.CommunityToolkit.Box2D;

/// <summary>
/// The settings every Box2D joint shares. A property left <see langword="null"/> keeps Box2D's
/// default for that joint type, so an empty options object is the same as passing none.
/// </summary>
public abstract record JointOptions2D
{
    /// <summary>Whether the two bodies still collide with each other. Box2D default: no.</summary>
    public bool? CollideConnected { get; init; }

    /// <summary>Force above which the joint raises a joint event, in newtons. Box2D default: never.</summary>
    public float? ForceThreshold { get; init; }

    /// <summary>Torque above which the joint raises a joint event, in newton-metres. Box2D default: never.</summary>
    public float? TorqueThreshold { get; init; }
}

/// <summary>
/// A hinge: the bodies share a pivot and turn about it. Optionally sprung toward a target angle,
/// limited to an angular range, or driven by a motor.
/// </summary>
public sealed record RevoluteJointOptions : JointOptions2D
{
    /// <summary>Angle the spring pulls toward, in radians from the pose at creation.</summary>
    public float? TargetAngle { get; init; }

    /// <summary>Turn the spring on.</summary>
    public bool? EnableSpring { get; init; }

    /// <summary>Spring stiffness, in hertz.</summary>
    public float? Hertz { get; init; }

    /// <summary>Spring damping ratio; 1 is critical.</summary>
    public float? DampingRatio { get; init; }

    /// <summary>Clamp the angle to <see cref="LowerAngle"/>..<see cref="UpperAngle"/>.</summary>
    public bool? EnableLimit { get; init; }

    /// <summary>Lower angular limit, in radians from the pose at creation.</summary>
    public float? LowerAngle { get; init; }

    /// <summary>Upper angular limit, in radians from the pose at creation.</summary>
    public float? UpperAngle { get; init; }

    /// <summary>Drive the hinge at <see cref="MotorSpeed"/>.</summary>
    public bool? EnableMotor { get; init; }

    /// <summary>The most torque the motor may apply, in newton-metres.</summary>
    public float? MaxMotorTorque { get; init; }

    /// <summary>Motor speed, in radians per second.</summary>
    public float? MotorSpeed { get; init; }
}

/// <summary>
/// A slider: body B moves along an axis fixed in body A, without turning relative to it.
/// </summary>
public sealed record PrismaticJointOptions : JointOptions2D
{
    /// <summary>Turn the spring on.</summary>
    public bool? EnableSpring { get; init; }

    /// <summary>Spring stiffness, in hertz.</summary>
    public float? Hertz { get; init; }

    /// <summary>Spring damping ratio; 1 is critical.</summary>
    public float? DampingRatio { get; init; }

    /// <summary>Translation the spring pulls toward, in metres along the axis.</summary>
    public float? TargetTranslation { get; init; }

    /// <summary>Clamp the translation to <see cref="LowerTranslation"/>..<see cref="UpperTranslation"/>.</summary>
    public bool? EnableLimit { get; init; }

    /// <summary>Lower translation limit, in metres along the axis.</summary>
    public float? LowerTranslation { get; init; }

    /// <summary>Upper translation limit, in metres along the axis.</summary>
    public float? UpperTranslation { get; init; }

    /// <summary>Drive the slider at <see cref="MotorSpeed"/>.</summary>
    public bool? EnableMotor { get; init; }

    /// <summary>The most force the motor may apply, in newtons.</summary>
    public float? MaxMotorForce { get; init; }

    /// <summary>Motor speed, in metres per second.</summary>
    public float? MotorSpeed { get; init; }
}

/// <summary>
/// A wheel on a suspension: the wheel turns freely about the pivot and slides along the axis,
/// usually sprung and limited, with a motor to drive it.
/// </summary>
public sealed record WheelJointOptions : JointOptions2D
{
    /// <summary>Turn the suspension spring on.</summary>
    public bool? EnableSpring { get; init; }

    /// <summary>Suspension stiffness, in hertz.</summary>
    public float? Hertz { get; init; }

    /// <summary>Suspension damping ratio; 1 is critical.</summary>
    public float? DampingRatio { get; init; }

    /// <summary>Clamp the suspension travel to <see cref="LowerTranslation"/>..<see cref="UpperTranslation"/>.</summary>
    public bool? EnableLimit { get; init; }

    /// <summary>Lower travel limit, in metres along the axis.</summary>
    public float? LowerTranslation { get; init; }

    /// <summary>Upper travel limit, in metres along the axis.</summary>
    public float? UpperTranslation { get; init; }

    /// <summary>Drive the wheel at <see cref="MotorSpeed"/>.</summary>
    public bool? EnableMotor { get; init; }

    /// <summary>The most torque the motor may apply, in newton-metres.</summary>
    public float? MaxMotorTorque { get; init; }

    /// <summary>Motor speed, in radians per second.</summary>
    public float? MotorSpeed { get; init; }
}

/// <summary>
/// A rod or a rope: keeps two anchors a set distance apart, or within a range, optionally sprung
/// or driven along its length.
/// </summary>
public sealed record DistanceJointOptions : JointOptions2D
{
    /// <summary>Rest length, in metres. Defaults to the distance between the anchors at creation.</summary>
    public float? Length { get; init; }

    /// <summary>Turn the spring on; the joint then behaves as a soft rod rather than a rigid one.</summary>
    public bool? EnableSpring { get; init; }

    /// <summary>Spring stiffness, in hertz.</summary>
    public float? Hertz { get; init; }

    /// <summary>Spring damping ratio; 1 is critical.</summary>
    public float? DampingRatio { get; init; }

    /// <summary>Lowest force the spring may apply, in newtons; negative pulls.</summary>
    public float? LowerSpringForce { get; init; }

    /// <summary>Highest force the spring may apply, in newtons.</summary>
    public float? UpperSpringForce { get; init; }

    /// <summary>Clamp the length to <see cref="MinLength"/>..<see cref="MaxLength"/> - a rope when the spring is on.</summary>
    public bool? EnableLimit { get; init; }

    /// <summary>Shortest allowed length, in metres.</summary>
    public float? MinLength { get; init; }

    /// <summary>Longest allowed length, in metres.</summary>
    public float? MaxLength { get; init; }

    /// <summary>Drive the length at <see cref="MotorSpeed"/>.</summary>
    public bool? EnableMotor { get; init; }

    /// <summary>The most force the motor may apply, in newtons.</summary>
    public float? MaxMotorForce { get; init; }

    /// <summary>Motor speed, in metres per second.</summary>
    public float? MotorSpeed { get; init; }
}

/// <summary>
/// Glues two bodies together. Zero hertz is rigid; a positive value makes the weld springy on
/// that axis, which is how breakable or soft assemblies are built.
/// </summary>
public sealed record WeldJointOptions : JointOptions2D
{
    /// <summary>Linear spring stiffness, in hertz; 0 is rigid.</summary>
    public float? LinearHertz { get; init; }

    /// <summary>Angular spring stiffness, in hertz; 0 is rigid.</summary>
    public float? AngularHertz { get; init; }

    /// <summary>Linear damping ratio.</summary>
    public float? LinearDampingRatio { get; init; }

    /// <summary>Angular damping ratio.</summary>
    public float? AngularDampingRatio { get; init; }
}

/// <summary>
/// Drives body B relative to body A: a velocity to chase with capped force and torque, and a
/// spring toward the frames' alignment. The mouse drag idiom, and the way to move a body without
/// making it kinematic.
/// </summary>
public sealed record MotorJointOptions : JointOptions2D
{
    /// <summary>Relative linear velocity to drive, in metres per second.</summary>
    public Vector2? LinearVelocity { get; init; }

    /// <summary>The most force the velocity drive may apply, in newtons.</summary>
    public float? MaxVelocityForce { get; init; }

    /// <summary>Relative angular velocity to drive, in radians per second.</summary>
    public float? AngularVelocity { get; init; }

    /// <summary>The most torque the velocity drive may apply, in newton-metres.</summary>
    public float? MaxVelocityTorque { get; init; }

    /// <summary>Linear spring stiffness toward the frames' alignment, in hertz.</summary>
    public float? LinearHertz { get; init; }

    /// <summary>Linear spring damping ratio.</summary>
    public float? LinearDampingRatio { get; init; }

    /// <summary>The most force the linear spring may apply, in newtons.</summary>
    public float? MaxSpringForce { get; init; }

    /// <summary>Angular spring stiffness, in hertz.</summary>
    public float? AngularHertz { get; init; }

    /// <summary>Angular spring damping ratio.</summary>
    public float? AngularDampingRatio { get; init; }

    /// <summary>The most torque the angular spring may apply, in newton-metres.</summary>
    public float? MaxSpringTorque { get; init; }
}