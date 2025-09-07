using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;

using var game = new Game();

game.Run(start: Start);

void Start(Scene rootScene)
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();

    var entity = game.Create3DPrimitive(PrimitiveModelType.Cube);
    entity.Transform.Position = new Vector3(-4f, 0.5f, 0);
    //entity.Add(new DebugRenderComponentScript());
    entity.Scene = rootScene;

    var entity5 = game.Create3DPrimitive(PrimitiveModelType.Cone, new() { Size = new(0.5f, 3, 0) });
    //entity5.Add(new CollidableGizmoScript() { Color = new Color4(0.25f, 0, 0, 0.5f) });
    entity5.Transform.Position = new Vector3(0, 2, 0);
    entity5.Scene = rootScene;

    var entity3 = game.Create3DPrimitive(PrimitiveModelType.Capsule);
    entity3.Transform.Position = new Vector3(0.01f, 6, 0);
    entity3.Scene = rootScene;

    var entity6 = game.Create3DPrimitive(PrimitiveModelType.Sphere);
    entity6.Transform.Position = new Vector3(0, 8, 0);
    entity6.Scene = rootScene;

    //var entity2 = game.Create3DPrimitive(PrimitiveModelType.Teapot, new()
    //{
    //    Material = game.CreateMaterial(Color.Blue)
    //});
    //entity2.Scene = rootScene;


    //var entity4 = game.Create3DPrimitive(PrimitiveModelType.Torus);
    //entity4.Transform.Position = new Vector3(0, 4, 0);
    //entity4.Scene = rootScene;
}