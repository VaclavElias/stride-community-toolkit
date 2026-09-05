// TEMPORARY - prints what Stride hands Bepu for this body, so the toolkit's numbers can be compared
// against the ones a bare Bepu simulation computes for the same hull.
using BepuPhysics;
using BepuPhysics.Collidables;
using Stride.BepuPhysics;

namespace Temp2DProbe;

internal sealed class ProbeBody : BodyComponent
{
    protected override void AttachInner(RigidPose pose, BodyInertia shapeInertia, TypedIndex shapeIndex)
    {
        var t = shapeInertia.InverseInertiaTensor;

        Console.Error.WriteLine(
            $"[attach] pos=({pose.Position.X:F4},{pose.Position.Y:F4},{pose.Position.Z:F4}) " +
            $"invMass={shapeInertia.InverseMass:E4} " +
            $"tensor=[XX {t.XX:E4} YX {t.YX:E4} YY {t.YY:E4} ZX {t.ZX:E4} ZY {t.ZY:E4} ZZ {t.ZZ:E4}]");
        Console.Error.Flush();

        base.AttachInner(pose, shapeInertia, shapeIndex);

        Console.Error.WriteLine(
            $"[attach] shapeType={shapeIndex.Type} shapeIndex={shapeIndex.Index}");
        Console.Error.Flush();
    }
}