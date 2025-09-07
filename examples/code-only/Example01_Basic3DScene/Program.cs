using Example01_Basic3DScene;
using Stride.BepuPhysics.Debug.Effects.RenderFeatures;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.ProceduralModels;

using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    game.SetupBase3DScene();
    game.AddSkybox();
    game.AddProfiler();

    AddRenderFeature();

    //var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);

    //entity.Transform.Position = new Vector3(0, 8, 0);

    //entity.Scene = rootScene;

    var entity = new Entity("test", new Vector3(1f, 0.5f, 3f))
                {
                    new ModelComponent(new CubeProceduralModel().Generate(game.Services)),
                    new RotationComponentScript()
                };

    entity.Scene = rootScene;

    //var entity2 = game.Create3DPrimitive(PrimitiveModelType.Teapot, new()
    //{
    //    Material = game.CreateMaterial(Color.Blue)
    //});
    //entity2.Scene = rootScene;

    var entity3 = game.Create3DPrimitive(PrimitiveModelType.Cube);
    entity3.Transform.Position = new Vector3(0, 2, 0);
    entity3.Scene = rootScene;

    //var entity4 = game.Create3DPrimitive(PrimitiveModelType.Torus);
    //entity4.Transform.Position = new Vector3(0, 4, 0);
    //entity4.Scene = rootScene;

    var entity5 = game.Create3DPrimitive(PrimitiveModelType.Cone);
    entity5.Add(new DebugBepuPhysicsShapes());
    entity5.Transform.Position = new Vector3(0, 6, 0);

    entity5.Scene = rootScene;

    var entity6 = game.Create3DPrimitive(PrimitiveModelType.Cone);
    entity6.Transform.Position = new Vector3(0, 8, 0);
    entity6.Scene = rootScene;

    var simulation = entity.GetSimulation();
});

void AddRenderFeature()
{
    game.SceneSystem.GraphicsCompositor.TryGetRenderStage("Opaque", out var opaqueRenderStage);
    var renderFeature = new SinglePassWireframeRenderFeature()
    {
        RenderStageSelectors =
        {
            new SimpleGroupToRenderStageSelector
            {
                EffectName = "RibbonBackground",
                RenderGroup = RenderGroupMask.All,
                RenderStage = opaqueRenderStage,
            }
        }
    };

    game.AddRootRenderFeature(renderFeature);
}