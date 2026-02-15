using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class ChoreCreatedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreCreated>
{
    public async Task Handle(ChoreCreated notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("ChoreCreated", new
            {
                choreId = notification.ChoreId,
                displayName = notification.DisplayName,
                description = notification.Description,
                isOptional = notification.IsOptional
            }, cancellationToken);
    }
}
