using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts ChoreCreated events to all connected household members.
/// </summary>
public class ChoreCreatedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreCreated>
{
    public async Task Handle(ChoreCreated notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("ChoreCreated", new
            {
                notification.ChoreId,
                notification.HouseholdId,
                notification.DisplayName,
                notification.Description,
                notification.Frequency,
                notification.IsOptional,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
