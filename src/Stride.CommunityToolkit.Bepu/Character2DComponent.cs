using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Stride.BepuPhysics;
using Stride.BepuPhysics.Components;
using Stride.BepuPhysics.Definitions.Contacts;
using Stride.Core;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Stride.Engine;
using NRigidPose = BepuPhysics.RigidPose;

namespace Stride.CommunityToolkit.Bepu;

[ComponentCategory("Physics - Bepu")]
public class Character2DComponent : BodyComponent, ISimulationUpdate, IContactEventHandler
{
    [DataMemberIgnore]
    public Vector3 Velocity { get; set; }

    /// <summary>
    /// Order is not guaranteed and may change at any moment
    /// </summary>
    [DataMemberIgnore]
    public List<(CollidableComponent Source, Contact Contact)> Contacts { get; } = [];

    public Character2DComponent()
    {
        InterpolationMode = BepuPhysics.Definitions.InterpolationMode.Interpolated;
    }

    /// <inheritdoc cref="BodyComponent.AttachInner"/>
    protected override void AttachInner(NRigidPose pose, BodyInertia shapeInertia, TypedIndex shapeIndex)
    {
        base.AttachInner(pose, new BodyInertia { InverseMass = 1f }, shapeIndex);
        FrictionCoefficient = 0f;
        ContactEventHandler = this;
    }

    /// <summary>
    /// Sets the velocity based on <paramref name="direction"/> and <see cref="Speed"/>
    /// </summary>
    /// <remarks>
    /// <paramref name="direction"/> does not have to be normalized;
    /// if the vector passed in has a length of 2, the character will go twice as fast
    /// </remarks>
    public virtual void Move(Vector3 direction)
    {
        Velocity = direction;
    }

    /// <summary>
    /// This is called internally right before the physics simulation does a tick
    /// </summary>
    /// <param name="sim"> The simulation this <see cref="ISimulationUpdate"/> is bound to, and the one currently updating </param>
    /// <param name="simTimeStep">The amount of time in seconds since the last simulation</param>
    public virtual void SimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        Awake = true; // Keep this body active
        LinearVelocity = Velocity;
    }

    private Dictionary<BodyComponent, float> BodyToZIndex = new();

    /// <summary>
    /// This is called internally right after the physics simulation does a tick
    /// </summary>
    /// <param name="sim"> The simulation this <see cref="ISimulationUpdate"/> is bound to, and the one currently updating </param>
    /// <param name="simTimeStep">The amount of time in seconds since the last simulation</param>
    public virtual void AfterSimulationUpdate(BepuSimulation sim, float simTimeStep)
    {
        for (int i = 0; i < sim.Simulation.Bodies.ActiveSet.Count; i++)
        {
            var handle = sim.Simulation.Bodies.ActiveSet.IndexToHandle[i];
            var body = sim.GetComponent(handle);

            if (BodyToZIndex.TryGetValue(body, out var ZIndex))
            {
                body.Position *= new Vector3(1, 1, 0);
                body.Position += new Vector3(0, 0, ZIndex);
            }
            else
            {
                BodyToZIndex.Add(body, body.Position.Z);
            }

            if (body.LinearVelocity.Z != 0)
                body.LinearVelocity *= new Vector3(1, 1, 0);

            var bodyRot = body.Orientation;
            Quaternion.RotationYawPitchRoll(ref bodyRot, out var yaw, out var pitch, out var roll);
            if (yaw != 0 || pitch != 0)
                body.Orientation = Quaternion.RotationYawPitchRoll(0, 0, roll);
            if (body.AngularVelocity.X != 0 || body.AngularVelocity.Y != 0)
                body.AngularVelocity *= new Vector3(0, 0, 1);
        }
    }

    bool IContactEventHandler.NoContactResponse => NoContactResponse;

    void IContactEventHandler.OnStartedTouching<TManifold>(CollidableComponent eventSource, CollidableComponent other, ref TManifold contactManifold, bool flippedManifold, int contactIndex, BepuSimulation bepuSimulation)
        => OnStartedTouching(eventSource, other, ref contactManifold, flippedManifold, contactIndex, bepuSimulation);
    void IContactEventHandler.OnStoppedTouching<TManifold>(CollidableComponent eventSource, CollidableComponent other, ref TManifold contactManifold, bool flippedManifold, int contactIndex, BepuSimulation bepuSimulation)
        => OnStoppedTouching(eventSource, other, ref contactManifold, flippedManifold, contactIndex, bepuSimulation);


    protected bool NoContactResponse => false;

    /// <inheritdoc cref="IContactEventHandler.OnStartedTouching{TManifold}"/>
    protected virtual void OnStartedTouching<TManifold>(CollidableComponent eventSource, CollidableComponent other, ref TManifold contactManifold, bool flippedManifold, int contactIndex, BepuSimulation bepuSimulation) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        contactManifold.GetContact(contactIndex, out var contact);

        if (flippedManifold)
        {
            // Contact manifold was computed from the other collidable's point of view, normal and offset should be flipped
            contact.Offset = -contact.Offset;
            contact.Normal = -contact.Normal;
        }

        contact.Offset = contact.Offset + Entity.Transform.WorldMatrix.TranslationVector + CenterOfMass;

        Contacts.Add((other, contact));
    }

    /// <inheritdoc cref="IContactEventHandler.OnStoppedTouching{TManifold}"/>
    protected virtual void OnStoppedTouching<TManifold>(CollidableComponent eventSource, CollidableComponent other, ref TManifold contactManifold, bool flippedManifold, int contactIndex, BepuSimulation bepuSimulation) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        for (int i = Contacts.Count - 1; i >= 0; i--)
        {
            if (Contacts[i].Source == other)
            {
                Contacts.SwapRemoveAt(i);
            }
        }
    }
}