using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class ChoreDeletedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreDeleted>
{
    public async Task Handle(ChoreDeleted notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("ChoreDeleted", new
            {
                choreId = notification.ChoreId
            }, cancellationToken);
    }
}
