using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

/// <summary>
/// Broadcasts MemberNicknameChanged events to all connected household members.
/// </summary>
public class MemberNicknameChangedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberNicknameChanged>
{
    public async Task Handle(MemberNicknameChanged notification, CancellationToken cancellationToken)
    {
        var groupName = HouseholdHub.GetGroupName(notification.HouseholdId);
        
        await hubContext.Clients
            .Group(groupName)
            .SendAsync("MemberNicknameChanged", new
            {
                notification.MemberId,
                notification.HouseholdId,
                notification.OldNickname,
                notification.NewNickname,
                notification.TimestampUtc
            }, cancellationToken);
    }
}
