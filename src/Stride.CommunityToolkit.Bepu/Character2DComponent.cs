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
    public float PlaneZ { get; set; } = 0f;

    /// <summary>
    /// Speculative margin used for this 2D body to reduce ghost contacts/instability with thin convex hulls.
    /// </summary>
    public float SpeculativeMargin2D { get; set; } = 0.02f;

    /// <summary>
    /// If true, enables passive CCD for convex hulls to reduce tunneling/instability when many contacts occur.
    /// </summary>
    public bool EnablePassiveCcdForConvexHulls { get; set; } = true;

    /// <summary>
    /// Strength (1/sec) of velocity-based correction that keeps the body on the Z plane. Avoids post-solve teleports.
    /// </summary>
    public float PlaneCorrectionStrength { get; set; } = 20f;

    /// <summary>
    /// If true, use a near-lock on X/Y rotation (very large inertia) instead of an exact lock (zero inverse inertia).
    /// This avoids singularities that can amplify impulses for sharp convex hulls.
    /// </summary>
    public bool UseSoftAngularLock { get; set; } = true;

    public Character2DComponent()
    {
        InterpolationMode = BepuPhysics.Definitions.InterpolationMode.Interpolated;
    }

    /// <inheritdoc cref="BodyComponent.AttachInner"/>
    protected override void AttachInner(NRigidPose pose, BodyInertia shapeInertia, TypedIndex shapeIndex)
    {
        // Keep the shape-derived inertia so rotation (including around Z) works.
        base.AttachInner(pose, shapeInertia, shapeIndex);

        // Constrain rotation to Z by heavily increasing inertia around X/Y (soft lock) or zeroing inverse inertia (hard lock).
        var inertia = BodyInertia;
        var inv = inertia.InverseInertiaTensor;
        if (UseSoftAngularLock)
        {
            // Set extremely small inverse inertia on X/Y (~infinite inertia) but nonzero to avoid ill-conditioned matrices.
            const float epsilon = 1e-6f;
            inv.XX = epsilon;
            inv.YY = epsilon;
        }
        else
        {
            inv.XX = 0f;
            inv.YY = 0f;
        }
        // Clear cross terms that could couple axes (leave ZZ untouched for roll).
        inv.YX = 0f; inv.ZX = 0f; inv.ZY = 0f;
        inertia.InverseInertiaTensor = inv;
        BodyInertia = inertia;

        // Reduce speculative margin to avoid explosive corrections with thin hulls in 2D.
        SpeculativeMargin = SpeculativeMargin2D;

        // Optionally enable CCD for convex hulls only, and damp recovery velocity for hulls (helps piles).
        if (HasConvexHull(Collider))
        {
            if (EnablePassiveCcdForConvexHulls)
                ContinuousDetectionMode = ContinuousDetectionMode.Passive;

            // Cap recovery velocity to keep depenetration impulses from spiking.
            MaximumRecoveryVelocity = MathF.Min(MaximumRecoveryVelocity, 1.5f);
            // Add some damping to help settling.
            SpringDampingRatio = MathF.Max(SpringDampingRatio, 1f);
            SpringFrequency = MathF.Min(SpringFrequency, 30f);
        }
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

        // Velocity-based plane correction instead of post-solve teleport to avoid energy injection.
        var zError = Position.Z - PlaneZ;
        current.Z = -zError * PlaneCorrectionStrength;

        LinearVelocity = current;
    }

    /// <summary>
    /// Called after the physics tick.
    /// </summary>
    public virtual void AfterSimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        // No hard teleports; the pre-step velocity correction handles the plane.
    }
}