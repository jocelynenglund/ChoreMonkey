using Microsoft.AspNetCore.SignalR;

namespace ChoreMonkey.Core.Infrastructure.SignalR;

/// <summary>
/// SignalR hub for real-time household updates.
/// Clients join a group per household to receive relevant events.
/// </summary>
public class HouseholdHub : Hub
{
    /// <summary>
    /// Join a household group to receive real-time updates for that household.
    /// </summary>
    public async Task JoinHousehold(Guid householdId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(householdId));
    }

    /// <summary>
    /// Leave a household group.
    /// </summary>
    public async Task LeaveHousehold(Guid householdId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(householdId));
    }

    public static string GetGroupName(Guid householdId) => $"household-{householdId}";
}
