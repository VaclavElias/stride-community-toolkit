using Box2D.NET;
using Stride.CommunityToolkit.Box2D;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Tests.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Xunit;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Shapes;

namespace Stride.CommunityToolkit.Tests.Box2D;

/// <summary>
/// Pins how <see cref="Box2DSimulation"/> steps and dispatches: a stall is dropped rather than replayed,
/// a handler may unregister itself from inside its callback, a short capsule becomes a circle as its mesh
/// does, and disposing twice is allowed.
/// </summary>
/// <remarks>
/// Box2D.NET keeps its worlds in process-wide state that is not safe to create and destroy from
/// several test classes at once, so every test that owns a world runs in the serialised collection.
/// </remarks>
[Collection(GameExtensionsRunTests.Name)]
public class SimulationSteppingTests
{
    // A frame a little longer than the 1/60 s fixed step: a TimeSpan of exactly 1/60 s is a tick short of it
    // and would run no step at all
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1 / 50.0);

    [Fact]
    public void AStall_IsCappedAndThenDropped_NotReplayed()
    {
        using var simulation = new Box2DSimulation();
        var counter = new StepCounter();

        simulation.RegisterSimulationUpdate(counter);

        // Two seconds in one frame: the cap allows three steps
        simulation.Update(TimeSpan.FromSeconds(2));

        Assert.Equal(3, counter.Steps);

        // The next ordinary frame is one step, not another three catching up on the stall
        simulation.Update(Frame);

        Assert.Equal(4, counter.Steps);
    }

    [Fact]
    public void ASimulationUpdateHandler_MayUnregisterItselfFromItsCallback()
    {
        using var simulation = new Box2DSimulation();
        var oneShot = new OneShotUpdate();
        var counter = new StepCounter();

        simulation.RegisterSimulationUpdate(oneShot);
        simulation.RegisterSimulationUpdate(counter);

        simulation.Update(Frame);
        simulation.Update(Frame);

        Assert.Equal(1, oneShot.Calls);
        Assert.Equal(2, counter.Steps);
    }

    [Fact]
    public void AContactHandler_MayUnregisterItselfFromItsCallback()
    {
        using var simulation = new Box2DSimulation();
        var ground = new Entity("ground");
        var box = new Entity("box");

        // Box2D.NET's default shape definition has every event off; the shape has to ask
        var def = ShapeFixtureBuilder.CreateDefaultShapeDef();
        def.enableContactEvents = true;

        StaticBoxFor(simulation, ground, new Vector2(0, -0.5f), halfWidth: 5f, halfHeight: 0.5f, def);

        // Resting on the ground from the first step, so the contact begins at once
        var body = simulation.CreateDynamicBody(box, new Vector3(0, 0.5f, 0));
        ShapeFixtureBuilder.AttachShape(Primitive2D.Rectangle(1f, 1f), body, def);

        var oneShot = new OneShotContact(simulation);

        simulation.RegisterContactEventHandler(oneShot);

        for (var i = 0; i < 10; i++) simulation.Update(Frame);

        Assert.Equal(1, oneShot.Calls);
    }

    [Fact]
    public void ACapsuleShorterThanTwoRadii_IsACircle_LikeItsMesh()
    {
        using var simulation = new Box2DSimulation();
        var body = simulation.CreateStaticBody(Vector3.Zero);

        // Box2D.NET attaches nothing for a capsule shorter than its slop, so the builder has to make the circle
        ShapeFixtureBuilder.AttachShape(Primitive2D.Capsule(radius: 0.5f, height: 0.5f), body);

        Assert.Equal(1, b2Body_GetShapeCount(body));

        Span<B2ShapeId> shapes = stackalloc B2ShapeId[1];
        b2Body_GetShapes(body, shapes, 1);

        Assert.Equal(B2ShapeType.b2_circleShape, b2Shape_GetType(shapes[0]));
    }

    [Fact]
    public void ACapsuleTallerThanTwoRadii_StaysACapsule()
    {
        using var simulation = new Box2DSimulation();
        var body = simulation.CreateStaticBody(Vector3.Zero);

        ShapeFixtureBuilder.AttachShape(Primitive2D.Capsule(radius: 0.25f, height: 1f), body);

        Span<B2ShapeId> shapes = stackalloc B2ShapeId[1];
        b2Body_GetShapes(body, shapes, 1);

        Assert.Equal(B2ShapeType.b2_capsuleShape, b2Shape_GetType(shapes[0]));
    }

    [Fact]
    public void Dispose_Twice_IsAllowed()
    {
        // More than one worker, so the task scheduler exists and is disposed too
        var simulation = new Box2DSimulation(new PhysicsStepSettings(WorkerCount: 2));

        simulation.Update(Frame);
        simulation.Dispose();
        simulation.Dispose();
    }

    private static void StaticBoxFor(Box2DSimulation simulation, Entity entity, Vector2 position, float halfWidth, float halfHeight, B2ShapeDef def)
    {
        var body = simulation.CreateStaticBody(entity, new Vector3(position, 0));

        ShapeFixtureBuilder.AttachShape(Primitive2D.Rectangle(halfWidth * 2, halfHeight * 2), body, def);
    }

    private sealed class StepCounter : IBox2DSimulationUpdate
    {
        public int Steps { get; private set; }

        public void AfterSimulationUpdate(Box2DSimulation simulation, float deltaTime) => Steps++;
    }

    private sealed class OneShotUpdate : IBox2DSimulationUpdate
    {
        public int Calls { get; private set; }

        public void AfterSimulationUpdate(Box2DSimulation simulation, float deltaTime)
        {
            Calls++;
            simulation.UnregisterSimulationUpdate(this);
        }
    }

    private sealed class OneShotContact(Box2DSimulation simulation) : IContactEventHandler
    {
        public int Calls { get; private set; }

        public void OnContactEvent(ContactEventData eventData)
        {
            Calls++;
            simulation.UnregisterContactEventHandler(this);
        }
    }
}