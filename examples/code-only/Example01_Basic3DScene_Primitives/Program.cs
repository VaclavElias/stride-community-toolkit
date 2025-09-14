using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;

var size1 = new Vector3(0.5f);
var size2 = new Vector3(0.25f, 0.5f, 0.25f);

using var game = new Game();

game.Run(start: Start);

void Start(Scene rootScene)
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();

    var cube = game.Create3DPrimitive(PrimitiveModelType.Cube,
        new() { Size = size1 });
    cube.Transform.Position = new Vector3(-4f, 0.5f, 0);
    //cube.Add(new DebugRenderComponentScript());
    //cube.Add(new CollidableGizmoScript()
    //{
    //    Color = new Color4(0.4f, 0.843f, 0, 0.9f),
    //    Visible = true
    //});
    cube.Scene = rootScene;

    var cuboid = game.Create3DPrimitive(PrimitiveModelType.Cube,
    new() { Size = size2 });
    cuboid.Transform.Position = new Vector3(-2.2f, 0.5f, -4f);
    cuboid.Scene = rootScene;

    var cone = game.Create3DPrimitive(PrimitiveModelType.Cone,
        new() { Size = new(0.5f, 3, 0) });
    //entity2.Add(new CollidableGizmoScript() { Color = new Color4(0.25f, 0, 0, 0.5f) });
    cone.Transform.Position = new Vector3(0, 2, 0);
    cone.Scene = rootScene;

    var capsule = game.Create3DPrimitive(PrimitiveModelType.Capsule,
        new() { Size = size2 });
    capsule.Transform.Position = new Vector3(0.01f, 6, 0);
    capsule.Scene = rootScene;

    var sphere = game.Create3DPrimitive(PrimitiveModelType.Sphere,
        new() { Size = size2 });
    sphere.Transform.Position = new Vector3(0, 8, 0);
    sphere.Scene = rootScene;

    var cylinder = game.Create3DPrimitive(PrimitiveModelType.Cylinder,
        new() { Size = size2 });
    cylinder.Transform.Position = new Vector3(0.5f, 10, 0);
    cylinder.Scene = rootScene;

    var teapot = game.Create3DPrimitive(PrimitiveModelType.Teapot);
    teapot.Transform.Position = new Vector3(4, 4f, 0);
    teapot.Scene = rootScene;

    var torus = game.Create3DPrimitive(PrimitiveModelType.Torus);
    torus.Transform.Position = new Vector3(0, 12, 0);
    torus.Scene = rootScene;

    //var entity4 = game.Create3DPrimitive(PrimitiveModelType.Torus);
    //entity4.Transform.Position = new Vector3(0, 4, 0);
    //entity4.Scene = rootScene;
}