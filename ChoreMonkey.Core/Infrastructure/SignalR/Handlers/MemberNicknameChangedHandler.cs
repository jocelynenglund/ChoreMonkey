using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class MemberNicknameChangedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<MemberNicknameChanged>
{
    public async Task Handle(MemberNicknameChanged notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("MemberNicknameChanged", new
            {
                memberId = notification.MemberId,
                oldNickname = notification.OldNickname,
                newNickname = notification.NewNickname
            }, cancellationToken);
    }
}
