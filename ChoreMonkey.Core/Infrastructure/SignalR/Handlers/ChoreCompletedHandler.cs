using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts ChoreCompleted events to all connected household members.
/// </summary>
public class ChoreCompletedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreCompleted>
{
    public async Task Handle(ChoreCompleted notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("ChoreCompleted", new
            {
                notification.ChoreId,
                notification.HouseholdId,
                notification.CompletedByMemberId,
                notification.CompletedAt,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
