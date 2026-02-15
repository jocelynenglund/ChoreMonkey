using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts ChoreDeleted events to all connected household members.
/// </summary>
public class ChoreDeletedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreDeleted>
{
    public async Task Handle(ChoreDeleted notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("ChoreDeleted", new
            {
                notification.ChoreId,
                notification.HouseholdId,
                notification.DeletedByMemberId,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
