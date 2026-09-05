using E13_SignalR_Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace E13_SignalR_Blazor.Hubs;

public class Screen1Hub : Hub<IScreenClient>
{
    public Task SendMessage(MessageDto dto)
        => Clients.All.ReceiveMessageAsync(dto);

    public Task SendCount(CountDto dto)
        => Clients.All.ReceiveCountAsync(dto);

    public Task SendUnitsRemoved(CountDto units)
        => Clients.All.ReceiveUnitsRemovedAsync(units);
}