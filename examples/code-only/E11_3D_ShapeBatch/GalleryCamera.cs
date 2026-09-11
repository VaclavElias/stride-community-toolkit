using Stride.CommunityToolkit.Engine;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace E11_3D_ShapeBatch;

/// <summary>
/// Moves the visitor. Given a pose it eases the camera there over <see cref="FlightDuration"/>
/// rather than cutting, and gives way the moment a hand touches the controls. It knows nothing
/// about the gallery: the poses are worked out by whoever asks for the flight.
/// </summary>
/// <remarks>
/// A flight only ever writes the camera transform, and the camera controller composes its own
/// movement onto whatever transform it finds, so the two never deadlock. Stride 4.4's scripts have
/// no enabled flag to switch off for the duration, and asking whether the visitor is steering is
/// better anyway - the flight yields instead of taking the controls away.
/// </remarks>
public sealed class GalleryCamera(Game game)
{
    /// <summary>How long a flight from one pose to another takes, in seconds.</summary>
    private const float FlightDuration = 0.7f;

    // Where the camera set off from, where it is going, and how far along it is. An elapsed time
    // past the duration means no flight is running.
    private Vector3 _from;
    private Vector3 _to;
    private Quaternion _fromRotation;
    private Quaternion _toRotation;
    private float _elapsed = float.MaxValue;

    /// <summary>Whether the camera is flying itself somewhere rather than being steered.</summary>
    public bool Flying => _elapsed < FlightDuration;

    /// <summary>Sends the camera to a pose, eased, or puts it there at once.</summary>
    public void FlyTo(Vector3 position, Quaternion rotation, bool instant)
    {
        var camera = game.GetCameraEntity().Transform;

        if (instant)
        {
            camera.Position = position;
            camera.Rotation = rotation;
            _elapsed = float.MaxValue;

            return;
        }

        _from = camera.Position;
        _fromRotation = camera.Rotation;
        _to = position;
        _toRotation = rotation;
        _elapsed = 0f;
    }

    /// <summary>Advances a flight in progress, if there is one and the visitor is not steering.</summary>
    public void Update(float deltaSeconds)
    {
        if (!Flying) return;

        // A hand on the controls outranks a flight: stop where we are and let the visitor steer
        if (BeingSteered)
        {
            _elapsed = float.MaxValue;

            return;
        }

        _elapsed += deltaSeconds;

        var t = MathUtil.Clamp(_elapsed / FlightDuration, 0f, 1f);

        // Smoothstep: the flight starts and ends at a standstill, which is what reads as a camera
        // moving rather than a cut. Slerp takes the short way round, so a flight from the last
        // station to the first turns the near way across the ring.
        var eased = t * t * (3f - 2f * t);
        var camera = game.GetCameraEntity().Transform;

        camera.Position = Vector3.Lerp(_from, _to, eased);
        camera.Rotation = Quaternion.Slerp(_fromRotation, _toRotation, eased);

        if (t < 1f) return;

        _elapsed = float.MaxValue;
    }

    /// <summary>Whether the visitor is steering: any key or button the camera controller reads.</summary>
    private bool BeingSteered
        => game.Input.IsMouseButtonDown(MouseButton.Right)
        || game.Input.IsKeyDown(Keys.W) || game.Input.IsKeyDown(Keys.A)
        || game.Input.IsKeyDown(Keys.S) || game.Input.IsKeyDown(Keys.D)
        || game.Input.IsKeyDown(Keys.Up) || game.Input.IsKeyDown(Keys.Down)
        || game.Input.IsKeyDown(Keys.Left) || game.Input.IsKeyDown(Keys.Right)
        || game.Input.IsKeyDown(Keys.Q) || game.Input.IsKeyDown(Keys.E);
}