//#:project ../../../src/Stride.CommunityToolkit.Bepu/Stride.CommunityToolkit.Bepu.csproj
//#:project ../../../src/Stride.CommunityToolkit.Skyboxes/Stride.CommunityToolkit.Skyboxes.csproj
//#:project ../../../src/Stride.CommunityToolkit.Windows/Stride.CommunityToolkit.Windows.csproj
#:package Stride.CommunityToolkit.Bepu@1.0.0-dev
#:package Stride.CommunityToolkit.Skyboxes@1.0.0-dev
#:package Stride.CommunityToolkit.Windows@1.0.0-dev

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

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);
    entity.Transform.Position = new Vector3(0, 8, 0);
    entity.Scene = rootScene;
}