using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class MemberRemovedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberRemoved>
{
    public async Task Handle(MemberRemoved notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("MemberRemoved", new
            {
                memberId = notification.MemberId,
                nickname = notification.Nickname
            }, cancellationToken);
    }
}
