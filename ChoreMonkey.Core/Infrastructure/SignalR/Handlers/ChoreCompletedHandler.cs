using ChoreMonkey.Events;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR.Handlers;

public class ChoreCompletedHandler(IHubContext<HouseholdHub> hubContext) 
    : INotificationHandler<ChoreCompleted>
{
    public async Task Handle(ChoreCompleted notification, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(notification.HouseholdId.ToString())
            .SendAsync("ChoreCompleted", new
            {
                choreId = notification.ChoreId,
                completedByMemberId = notification.CompletedByMemberId,
                completedAt = notification.CompletedAt
            }, cancellationToken);
    }
}
