using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class MemberStatusChangedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberStatusChanged>
{
    public async Task Handle(MemberStatusChanged notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("MemberStatusChanged", new
            {
                memberId = notification.MemberId,
                status = notification.Status
            }, cancellationToken);
    }
}
