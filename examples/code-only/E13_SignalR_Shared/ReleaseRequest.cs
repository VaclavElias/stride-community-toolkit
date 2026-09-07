namespace E13_SignalR_Shared;

/// <summary>A request to drop one container. Either field left <see langword="null"/> is chosen at random by the game.</summary>
public sealed record ReleaseRequest(ContainerSize? Size = null, ContainerPaint? Paint = null);