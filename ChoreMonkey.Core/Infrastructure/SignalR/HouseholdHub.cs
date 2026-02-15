using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR;

public class HouseholdHub : Hub
{
    public async Task JoinHousehold(string householdId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, householdId);
    }

    public async Task LeaveHousehold(string householdId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, householdId);
    }
}
