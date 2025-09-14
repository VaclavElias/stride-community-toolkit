using BepuPhysics;
using BepuPhysics.Collidables;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Components;
using Stride.BepuPhysics.Definitions.Colliders;
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
    public float PlaneZ { get; set; } = 0.1f;

    /// <summary>
    /// Speculative margin used for this 2D body to reduce ghost contacts/instability with thin convex hulls.
    /// </summary>
    public float SpeculativeMargin2D { get; set; } = 0.02f;

    /// <summary>
    /// If true, enables passive CCD for convex hulls to reduce tunneling/instability when many contacts occur.
    /// </summary>
    public bool EnablePassiveCcdForConvexHulls { get; set; } = true;

    public Character2DComponent()
    {
        InterpolationMode = BepuPhysics.Definitions.InterpolationMode.Interpolated;
    }

    /// <inheritdoc cref="BodyComponent.AttachInner"/>
    protected override void AttachInner(NRigidPose pose, BodyInertia shapeInertia, TypedIndex shapeIndex)
    {
        // Keep the shape-derived inertia so rotation (including around Z) works.
        base.AttachInner(pose, shapeInertia, shapeIndex);

        // Constrain rotation to Z by locking X/Y inverse inertia.
        var inertia = BodyInertia;
        var inv = inertia.InverseInertiaTensor;
        inv.XX = 0f; inv.YY = 0f;
        inv.YX = 0f; inv.ZX = 0f; inv.ZY = 0f; // clear cross terms that could coupling axes
        inertia.InverseInertiaTensor = inv;
        BodyInertia = inertia;

        // Reduce speculative margin to avoid explosive corrections with thin hulls in 2D.
        SpeculativeMargin = SpeculativeMargin2D;

        // Optionally enable CCD for convex hulls only.
        if (EnablePassiveCcdForConvexHulls && HasConvexHull(Collider))
            ContinuousDetectionMode = ContinuousDetectionMode.Passive;
    }

    private static bool HasConvexHull(ICollider? collider)
    {
        if (collider is null) return false;
        if (collider is ConvexHullCollider) return true;
        if (collider is CompoundCollider compound && compound.Colliders is { } list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is ConvexHullCollider) return true;
            }
        }
        return false;
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
        // Constrain this body to the XY plane.
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

        // For 2D, kill X/Y angular velocity so only Z rotation remains (allows rolling).
        if (AngularVelocity.X != 0 || AngularVelocity.Y != 0)
            AngularVelocity = new Vector3(0, 0, AngularVelocity.Z);
    }
}