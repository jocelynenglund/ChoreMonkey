using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.AssignChore;

/// <summary>Assign Chore: records who the chore is for. Nothing is checked (not even that the chore exists).</summary>
public static class AssignChoreSlice
{
    public static Decision Decide(IReadOnlyList<object> history, AssignChoreCommand cmd) =>
        Decision.Accept(new ChoreAssigned(cmd.ChoreId, cmd.HouseholdId, cmd.MemberIds, cmd.AssignToAll));
}
