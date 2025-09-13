using Example17_SignalR.Builders;
using Example17_SignalR.Core;
using Example17_SignalR.Services;
using Stride.CommunityToolkit.Engine;
using Stride.Engine;

using var game = new Game();

game.Services.AddService(new ScreenService($"{GameSettings.HubBaseUrl}/{GameSettings.HubUrl}"));

game.Run(start: (Scene rootScene) =>
{
    SceneBuilder.Build(game, rootScene);
});