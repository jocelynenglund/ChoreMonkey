using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class MemberJoinedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberJoinedHousehold>
{
    public async Task Handle(MemberJoinedHousehold notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("MemberJoined", new
            {
                memberId = notification.MemberId,
                nickname = notification.Nickname
            }, cancellationToken);
    }
}
