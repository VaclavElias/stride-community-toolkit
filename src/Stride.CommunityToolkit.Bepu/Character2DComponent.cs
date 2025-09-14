using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Components;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using NRigidPose = BepuPhysics.RigidPose;

namespace Stride.CommunityToolkit.Bepu;

[ComponentCategory("Physics - Bepu")]
public class Character2DComponent : BodyComponent, ISimulationUpdate
{
    [DataMemberIgnore]
    public Vector3 Velocity { get; set; }

    /// <summary>
    /// Apply input X each tick. Leave off for passive bodies so physics can freely control motion.
    /// </summary>
    public bool UseInputHorizontalVelocity { get; set; } = false;

    /// <summary>
    /// When true, the Y component from <see cref="Velocity"/> will be applied each tick. Otherwise gravity/contacts control Y.
    /// </summary>
    public bool UseInputVerticalVelocity { get; set; } = false;

    /// <summary>
    /// Target Z plane to constrain this body to (2D).
    /// </summary>
    public float PlaneZ { get; set; } = 0f;

    public Character2DComponent()
    {
        InterpolationMode = BepuPhysics.Definitions.InterpolationMode.Interpolated;
    }

    /// <inheritdoc cref="BodyComponent.AttachInner"/>
    protected override void AttachInner(NRigidPose pose, BodyInertia shapeInertia, TypedIndex shapeIndex)
    {
        // IMPORTANT: keep the shape-derived inertia so rotation (including around Z) works.
        base.AttachInner(pose, shapeInertia, shapeIndex);
    }

    /// <summary>
    /// Sets the desired input velocity (planar X and optional Y).
    /// </summary>
    public virtual void Move(Vector3 direction)
    {
        Velocity = direction;
    }

    /// <summary>
    /// Called before the physics tick.
    /// </summary>
    public virtual void SimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        Awake = true; // Keep this body active

        var current = LinearVelocity;
        var input = Velocity;

        if (UseInputHorizontalVelocity)
            current.X = input.X;
        if (UseInputVerticalVelocity)
            current.Y = input.Y;
        current.Z = 0f; // keep in XY plane

        LinearVelocity = current;
    }

    /// <summary>
    /// Called after the physics tick.
    /// </summary>
    public virtual void AfterSimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        // Constrain this body to the XY plane and orientation around Z only.
        if (Position.Z != PlaneZ)
        {
            var p = Position;
            p.Z = PlaneZ;
            Position = p;
        }

        if (LinearVelocity.Z != 0)
        {
            var lv = LinearVelocity;
            lv.Z = 0f;
            LinearVelocity = lv;
        }

        // Keep yaw/pitch at 0 (roll-only). If they are already 0, this block is skipped.
        var bodyRot = Orientation;
        Quaternion.RotationYawPitchRoll(ref bodyRot, out var yaw, out var pitch, out var roll);
        if (yaw != 0 || pitch != 0)
            Orientation = Quaternion.RotationYawPitchRoll(0, 0, roll);

        // For 2D, kill X/Y angular velocity so only Z rotation remains (allows rolling).
        if (AngularVelocity.X != 0 || AngularVelocity.Y != 0)
            AngularVelocity = new Vector3(0, 0, AngularVelocity.Z);
    }
}