using E13_SignalR_Shared;
using Stride.BepuPhysics;
using Stride.Engine;

namespace E13_SignalR.Station;

/// <summary>One container on (or above, or below) the deck, and what the deck needs to know about it.</summary>
public sealed class Container
{
    public required int Id { get; init; }

    public required ContainerSize Size { get; init; }

    public required ContainerPaint Paint { get; init; }

    /// <summary>Which console released it. Carried through every event so the web feed can say "from web".</summary>
    public required CommandOrigin Origin { get; init; }

    public required Entity Entity { get; init; }

    public required BodyComponent Body { get; init; }

    /// <summary>Deck time at release, for the air time that goes out with the landing report.</summary>
    public required float ReleasedAt { get; init; }

    /// <summary>Set once the landing has been reported, so it is reported once.</summary>
    public bool Landed { get; set; }

    public float Mass => ContainerFactory.Spec(Size).Mass;

    public ContainerEvent ToEvent(Point3? position = null, float? airTime = null)
        => new(Id, Size, Paint, Origin, position, airTime);
}