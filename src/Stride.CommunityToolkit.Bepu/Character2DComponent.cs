using BepuPhysics;
using BepuPhysics.Collidables;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Components;
using Stride.BepuPhysics.Definitions.Colliders;
using Stride.Engine;
using NRigidPose = BepuPhysics.RigidPose;

namespace Stride.CommunityToolkit.Bepu;

[ComponentCategory("Physics - Bepu 2D")]
public class Character2DComponent : BodyComponent, ISimulationUpdate
{
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
        var inverseInertia = inertia.InverseInertiaTensor;

        inverseInertia.XX = 0f;
        inverseInertia.YY = 0f;

        // Clear cross terms that could couple axes (leave ZZ untouched for roll).
        inverseInertia.YX = 0f; inverseInertia.ZX = 0f; inverseInertia.ZY = 0f;
        inertia.InverseInertiaTensor = inverseInertia;

        BodyInertia = inertia;

        if (HasConvexHull(Collider))
        {
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
    /// Called before the physics tick.
    /// </summary>
    public virtual void SimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        // Keep this body active
        Awake = true;

        var current = LinearVelocity;

        var zError = Position.Z;
        current.Z = -zError;

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