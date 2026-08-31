using Stride.CommunityToolkit.Box2D;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;


Box2DSimulation? simulation = null;

using var game = new Game();

game.Run(start: Start);

void Start(Scene rootScene)
{
    // Configure the game window
    game.Window.AllowUserResizing = true;

    game.SetupBase2D(clearColor: new Color(0.2f));
    game.Add2DCameraController();
    game.AddProfiler();


    // Initialize the Box2D physics simulation
    simulation = new Box2DSimulation();
}