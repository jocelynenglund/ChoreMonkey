using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.AddChore;

/// <summary>Add Chore: records a ChoreCreated, "once" when no frequency is given. Nothing is checked (not even a repeated id).</summary>
public static class AddChoreSlice
{
    public static Decision Decide(IReadOnlyList<object> history, AddChoreCommand cmd) =>
        Decision.Accept(new ChoreCreated(
            cmd.ChoreId,
            cmd.HouseholdId,
            cmd.DisplayName,
            cmd.Description,
            cmd.Frequency ?? new ChoreFrequency("once"),
            cmd.IsOptional,
            cmd.StartDate,
            cmd.IsRequired,
            cmd.MissedDeduction));
}
