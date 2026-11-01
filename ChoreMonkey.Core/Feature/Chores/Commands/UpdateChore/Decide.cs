using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.UpdateChore;

/// <summary>Update Chore: the chore must exist and not be deleted.</summary>
public static class UpdateChoreSlice
{
    public const string NotFound = "Chore not found.";

    public static Decision Decide(IReadOnlyList<object> history, UpdateChoreCommand cmd)
    {
        var exists = history.OfType<ChoreCreated>().Any(e => e.ChoreId == cmd.ChoreId);
        var deleted = history.OfType<ChoreDeleted>().Any(e => e.ChoreId == cmd.ChoreId);
        if (!exists || deleted) return Decision.NotFound(NotFound);

        return Decision.Accept(new ChoreUpdated(
            cmd.ChoreId,
            cmd.HouseholdId,
            cmd.DisplayName,
            cmd.Description,
            cmd.Frequency,
            cmd.IsOptional,
            cmd.StartDate,
            cmd.IsRequired,
            cmd.MissedDeduction));
    }
}
