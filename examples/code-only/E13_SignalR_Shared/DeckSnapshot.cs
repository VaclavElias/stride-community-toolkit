namespace E13_SignalR_Shared;

/// <summary>
/// The deck census the game sends once a second. A page that opens late is complete after the first
/// one, which is why the hub keeps no state of its own. Counts are arrays in enum order rather than
/// dictionaries, so the page indexes them with the enum and nothing has to agree on key formatting.
/// </summary>
public sealed record DeckSnapshot(
    int OnDeck,
    int Released,
    int Lost,
    float TotalMass,
    string Scheme,
    float UptimeSeconds,
    int[] BySize,
    int[] ByPaint);