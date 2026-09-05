using Stride.Audio;
using Stride.Engine;
using System.Runtime.CompilerServices;

namespace Stride.CommunityToolkit.Audio;

/// <summary>
/// The two internal fields of the engine's audio types that runtime sound cannot do without.
/// </summary>
/// <remarks>
/// <para>
/// <b>The listener.</b> The engine moves only the listeners that belong to an
/// <see cref="AudioListenerComponent"/>: its processor copies the entity's position, orientation
/// and velocity into the listener every frame. The <see cref="AudioEngine.DefaultListener"/>, the
/// one a runtime <see cref="SoundInstance"/> gets by default, is never updated by anything and
/// sits at the origin facing +Z. So a sound that should follow the camera needs the component's
/// listener - and the field holding it is internal.
/// </para>
/// <para>
/// <b>The emitter's world transform.</b> <see cref="AudioEmitter"/> exposes position, forward, up
/// and velocity, and the plain 3D path uses those. The HRTF path on XAudio2 does not: it takes the
/// emitter's internal <c>WorldTransform</c> matrix, multiplies it by the inverse of the listener's,
/// and hands the result to the HRTF processor. Left at its default of all zeros, the processor
/// receives a degenerate orientation and produces silence. The engine's own emitter processor
/// writes that field; <see cref="SoundEmitterScript"/> has to as well.
/// </para>
/// <para>
/// <see cref="UnsafeAccessorAttribute"/> reads the fields without reflection cost. If a future
/// engine renames one, the first call throws <see cref="MissingFieldException"/> rather than
/// returning something wrong.
/// </para>
/// </remarks>
internal static class AudioInternals
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Listener")]
    private static extern ref AudioListener? ListenerField(AudioListenerComponent component);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "WorldTransform")]
    private static extern ref Matrix WorldTransformField(AudioEmitter emitter);

    /// <summary>
    /// The component's listener, or <see langword="null"/> until the entity has been processed -
    /// the engine creates it when the component enters a scene.
    /// </summary>
    public static AudioListener? GetListener(AudioListenerComponent component) => ListenerField(component);

    /// <summary>
    /// Sets the emitter's world transform, which the HRTF path positions from.
    /// </summary>
    public static void SetWorldTransform(AudioEmitter emitter, in Matrix worldTransform) => WorldTransformField(emitter) = worldTransform;
}