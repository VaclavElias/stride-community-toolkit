using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Mathematics;
using Stride.Engine;
using Stride.Input;

namespace Example.Common.Galleries;

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
    /// <summary>How long a flight between stations takes; a flight home may ask for longer.</summary>
    public const float FlightDuration = 0.7f;

    // Where the camera set off from, where it is going, and how far along it is. An elapsed time
    // past the duration means no flight is running.
    private Vector3 _from;
    private Vector3 _to;
    private Quaternion _fromRotation;
    private Quaternion _toRotation;
    private float _elapsed = float.MaxValue;
    private float _duration = FlightDuration;
    private float _lift;
    private Vector3? _facing;

    /// <summary>Whether the camera is flying itself somewhere rather than being steered.</summary>
    public bool Flying => _elapsed < _duration;

    /// <summary>Sends the camera to a pose, eased, or puts it there at once.</summary>
    /// <param name="position">Where the camera ends up.</param>
    /// <param name="rotation">Which way it looks when it gets there.</param>
    /// <param name="instant">Put it there at once, rather than flying it.</param>
    /// <summary>Flies the camera to a pose, or puts it there.</summary>
    /// <param name="position">Where to end up.</param>
    /// <param name="rotation">How to face on arrival.</param>
    /// <param name="instant">Put the camera there at once, rather than flying it.</param>
    /// <param name="duration">How long the flight takes, in seconds.</param>
    /// <param name="lift">How far the path arcs upward at its middle, in world units; 0 flies straight.</param>
    /// <param name="facing">A point to keep facing on the way: the camera turns to it over the first quarter of the flight and settles onto <paramref name="rotation"/> as it arrives.</param>
    public void FlyTo(Vector3 position, Quaternion rotation, bool instant, float duration = FlightDuration, float lift = 0f, Vector3? facing = null)
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
        _duration = MathF.Max(duration, 0.01f);
        _lift = lift;
        _facing = facing;
        _elapsed = 0f;
    }

    /// <summary>Advances a flight in progress, if there is one and the visitor is not steering.</summary>
    /// <param name="deltaSeconds">The frame's elapsed time.</param>
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

        var t = MathUtil.Clamp(_elapsed / _duration, 0f, 1f);

        // Smoothstep: the flight starts and ends at a standstill, which is what reads as a camera
        // moving rather than a cut. Slerp takes the short way round, so a flight from the last
        // station to the first turns the near way across the ring.
        var eased = Easing.SmoothStep(t);
        var camera = game.GetCameraEntity().Transform;

        // The lift is a half sine over the flight: up and over rather than through the exhibits
        var position = Vector3.Lerp(_from, _to, eased) + Vector3.UnitY * (_lift * MathF.Sin(t * MathF.PI));
        camera.Position = position;

        if (_facing is { } focus)
        {
            // Turn to the focus over the first quarter, hold it through the middle, and settle onto
            // the final rotation towards the end - squared, so the ring stays in view for longer
            var towards = LookRotation(focus - position);
            var turned = Quaternion.Slerp(_fromRotation, towards, MathUtil.Clamp(t / 0.25f, 0f, 1f));

            camera.Rotation = Quaternion.Slerp(turned, _toRotation, eased * eased);
        }
        else
        {
            camera.Rotation = Quaternion.Slerp(_fromRotation, _toRotation, eased);
        }

        if (t < 1f) return;

        _elapsed = float.MaxValue;
    }

    /// <summary>The rotation that looks along a direction, level: a camera looks down its -Z, yaw turns that towards -X, pitch lifts it.</summary>
    /// <param name="direction">Where to look; need not be normalised.</param>
    public static Quaternion LookRotation(Vector3 direction)
    {
        direction.Normalize();

        var yaw = MathF.Atan2(-direction.X, -direction.Z);
        var pitch = MathF.Asin(MathUtil.Clamp(direction.Y, -1f, 1f));

        return Quaternion.RotationYawPitchRoll(yaw, pitch, 0f);
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