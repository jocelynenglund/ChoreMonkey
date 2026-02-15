using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts MemberJoinedHousehold events to all connected household members.
/// </summary>
public class MemberJoinedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberJoinedHousehold>
{
    public async Task Handle(MemberJoinedHousehold notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("MemberJoined", new
            {
                notification.MemberId,
                notification.HouseholdId,
                notification.Nickname,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
