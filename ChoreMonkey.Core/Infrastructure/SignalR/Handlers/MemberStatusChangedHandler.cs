using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts MemberStatusChanged events to all connected household members.
/// </summary>
public class MemberStatusChangedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberStatusChanged>
{
    public async Task Handle(MemberStatusChanged notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("MemberStatusChanged", new
            {
                notification.MemberId,
                notification.HouseholdId,
                notification.Status,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
