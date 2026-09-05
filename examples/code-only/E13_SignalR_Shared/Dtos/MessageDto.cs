using E13_SignalR_Shared.Core;

namespace E13_SignalR_Shared.Dtos;

public class MessageDto
{
    public EntityType Type { get; set; }
    public required string Text { get; set; }
}