using Example01_Basic3DScene;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.Compositing;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.CommunityToolkit.Skyboxes;
using Stride.Core.Mathematics;
using Stride.Engine;

using var game = new Game();

// Note: CameraOrientationGizmo

game.Run(start: (Scene rootScene) =>
{
    //game.SetupBase3DScene();
    game.AddGraphicsCompositor().AddCleanUIStage().AddSceneRenderer(new MyCustomRenderer());
    game.Add3DCamera().Add3DCameraController();
    game.AddDirectionalLight();
    game.Add3DGround();

    game.AddSkybox();
    game.AddProfiler();

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);

    entity.Transform.Position = new Vector3(0, 8, 0);

    entity.Scene = rootScene;

    entity.Add(new SpriteBatchRenderer());

    //var myFont = game.Content.Load<SpriteFont>("StrideDefaultFont");

    //AddSpriteBatchRenderer(rootScene);
});