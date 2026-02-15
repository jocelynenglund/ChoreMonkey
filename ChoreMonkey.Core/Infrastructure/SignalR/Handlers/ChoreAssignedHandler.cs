using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class ChoreAssignedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreAssigned>
{
    public async Task Handle(ChoreAssigned notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("ChoreAssigned", new
            {
                choreId = notification.ChoreId,
                assignedToMemberIds = notification.AssignedToMemberIds,
                assignToAll = notification.AssignToAll
            }, cancellationToken);
    }
}
