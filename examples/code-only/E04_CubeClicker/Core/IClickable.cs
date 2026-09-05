using Stride.Input;

namespace E04_CubeClicker.Core;

/// <summary>
/// Interfaces aren't [DataContract] tagged.
/// An Implementing class will add it on registration to the YamlRegistry
/// </summary>
public interface IClickable
{
    string Prefix { get; init; }
    int Count { get; set; }
    MouseButton Type { get; }
    void HandleClick();
}