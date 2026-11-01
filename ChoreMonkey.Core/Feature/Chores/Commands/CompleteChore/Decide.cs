using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.CompleteChore;

/// <summary>
/// Complete Chore: a member who isn't assigned is assigned first (ChoreAssigned, then ChoreCompleted),
/// keeping the earlier assignees and the assign-to-all flag. Nothing else is checked.
/// </summary>
public static class CompleteChoreSlice
{
    public static Decision Decide(IReadOnlyList<object> history, CompleteChoreCommand cmd, DateTime now)
    {
        var completedAt = cmd.CompletedAt ?? now;

        var currentAssignment = history
            .OfType<ChoreAssigned>()
            .Where(e => e.ChoreId == cmd.ChoreId)
            .LastOrDefault();

        var isAssigned = currentAssignment?.AssignToAll == true ||
            (currentAssignment?.AssignedToMemberIds?.Contains(cmd.MemberId) ?? false);

        var completed = new ChoreCompleted(cmd.ChoreId, cmd.HouseholdId, cmd.MemberId, completedAt);
        if (isAssigned) return Decision.Accept(completed);

        var newAssignees = currentAssignment?.AssignedToMemberIds?.ToList() ?? new List<Guid>();
        newAssignees.Add(cmd.MemberId);
        var assigned = new ChoreAssigned(cmd.ChoreId, cmd.HouseholdId, newAssignees.ToArray(), currentAssignment?.AssignToAll ?? false);
        return Decision.Accept(assigned, completed);
    }
}
