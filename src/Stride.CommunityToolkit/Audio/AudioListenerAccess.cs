using Stride.Audio;
using Stride.Engine;
using System.Runtime.CompilerServices;

namespace Stride.CommunityToolkit.Audio;

/// <summary>
/// Reads the <see cref="AudioListener"/> the engine keeps on an <see cref="AudioListenerComponent"/>.
/// </summary>
/// <remarks>
/// <para>
/// The engine moves only the listeners that belong to an <see cref="AudioListenerComponent"/>: its
/// processor copies the entity's position, orientation and velocity into the listener every frame.
/// The <see cref="AudioEngine.DefaultListener"/>, the one a runtime <see cref="SoundInstance"/>
/// gets by default, is never updated by anything and sits at the origin facing +Z. So a sound that
/// should follow the camera needs the component's listener - and the field holding it is internal.
/// </para>
/// <para>
/// <see cref="UnsafeAccessorAttribute"/> reads that field without reflection cost. If a future
/// engine renames it, the first call throws <see cref="MissingFieldException"/> rather than
/// returning something wrong.
/// </para>
/// </remarks>
internal static class AudioListenerAccess
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Listener")]
    private static extern ref AudioListener? ListenerField(AudioListenerComponent component);

    /// <summary>
    /// The component's listener, or <see langword="null"/> until the entity has been processed -
    /// the engine creates it when the component enters a scene.
    /// </summary>
    public static AudioListener? GetListener(AudioListenerComponent component) => ListenerField(component);
}