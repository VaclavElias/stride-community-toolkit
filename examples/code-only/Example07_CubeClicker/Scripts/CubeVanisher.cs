using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Example07_CubeClicker.Scripts;

public class CubeVanisher : AsyncScript
{
    private const float TotalTime = 0.5f;
    private const float RotationSpeed = 900;

    public override async Task Execute()
    {
        var elapsedTime = 0f;

        while (elapsedTime < TotalTime)
        {
            elapsedTime += (float)Game.UpdateTime.Elapsed.TotalSeconds;

            Entity.Transform.Scale = new Vector3(1 - elapsedTime / TotalTime);
            Entity.Transform.Rotation = Quaternion.RotationY(MathUtil.DegreesToRadians(RotationSpeed * elapsedTime));

            await Script.NextFrame();
        }

        Entity.Remove();

        Console.WriteLine("Entity removed");
    }
}