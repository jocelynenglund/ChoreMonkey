using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts ChoreAssigned events to all connected household members.
/// </summary>
public class ChoreAssignedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreAssigned>
{
    public async Task Handle(ChoreAssigned notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("ChoreAssigned", new
            {
                notification.ChoreId,
                notification.HouseholdId,
                notification.AssignedToMemberIds,
                notification.AssignToAll,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
