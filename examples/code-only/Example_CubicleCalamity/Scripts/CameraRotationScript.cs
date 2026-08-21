using Example_CubicleCalamity.Shared;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Example_CubicleCalamity.Scripts;

public class CameraRotationScript : SyncScript
{
    private float _rotationSpeed = 45f; // degrees per second
    private Vector3 _rotationCentre;
    DebugOverlaySection? _instructions;

    /// <summary>
    /// Gets or sets the world point the camera orbits and aims at. Leave <see langword="null"/> to
    /// fall back to the ground entity's position.
    /// </summary>
    /// <remarks>
    /// The ground sits at Y = 0, so orbiting around it alone aims the camera at the base of the
    /// platform. Pointing this at the platform's mid-height keeps the stack framed as it turns.
    /// </remarks>
    public Vector3? RotationCentre { get; set; }

    public override void Start()
    {
        // Falls back to the ground, then to the origin - previously a missing ground returned early
        // and silently took the instructions overlay down with it
        _rotationCentre = RotationCentre
            ?? SceneSystem.SceneInstance.RootScene
                   .Entities.FirstOrDefault(e => e.Name == EntityNames.Ground)?.Transform.Position
            ?? Vector3.Zero;

        InitializeDebugOverlay();
    }

    public override void Update()
    {

        // Compute how many degrees we should turn this frame
        var deltaTime = this.DeltaTime();

        float deltaRotation = 0f;

        if (Input.IsKeyDown(Keys.Z))
        {
            deltaRotation = -_rotationSpeed * deltaTime;
        }
        else if (Input.IsKeyDown(Keys.C))
        {
            deltaRotation = +_rotationSpeed * deltaTime;
        }

        if (Math.Abs(deltaRotation) > 0.001f)
        {
            RotateAroundCentre(deltaRotation);
        }
    }

    private void RotateAroundCentre(float angleDegrees)
    {
        // Compute offset from centre
        var offset = Entity.Transform.Position - _rotationCentre;

        // Rotate offset around world‑Y
        var yawQuat = Quaternion.RotationY(MathUtil.DegreesToRadians(angleDegrees));
        var rotatedOffset = Vector3.Transform(offset, yawQuat);

        // Reposition the camera
        Entity.Transform.Position = _rotationCentre + rotatedOffset;

        // Re‑aim the camera at the centre (preserves original pitch/tilt)
        Entity.Transform.LookAt(_rotationCentre, Vector3.UnitY);
    }

    void InitializeDebugOverlay()
    {
        var overlay = DebugOverlay.GetOrCreate(Game);

        overlay.Position = DisplayPosition.BottomLeft;

        // Runs every frame the overlay is drawn, so the camera position readout stays live
        _instructions = overlay.AddSection(
            "Game", () => GenerateInstructions(Entity.Transform.Position));
    }

    static List<TextElement> GenerateInstructions(Vector3 cameraPosition)
     => [
            new("GAME INSTRUCTIONS"),
            //new("Click the golden sphere and drag to move it (Y-axis locked)"),
            new("Click a cube", Color.Yellow),
            new("Hold Shift: Left mouse button down", Color.Yellow),
            new("Z/C orbit around the platform", Color.Yellow),
            new($"Camera Position: {cameraPosition}", Color.Yellow),
        ];
}