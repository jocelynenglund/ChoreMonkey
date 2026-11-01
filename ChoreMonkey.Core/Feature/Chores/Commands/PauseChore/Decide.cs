using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.PauseChore;

/// <summary>
/// Pause Chore: admin PIN needed (Forbidden), the end must not be before the start (Invalid, whole days),
/// and the chore must exist and not be deleted (Invalid, as today). History is the household's events and the chores' events together.
/// </summary>
public static class PauseChoreSlice
{
    public const string HouseholdNotFound = "Household not found";
    public const string AdminRequired = "Admin access required";
    public const string EndBeforeStart = "Pause end date must be on or after the start date";
    public const string ChoreNotFound = "Chore not found";

    public static Decision Decide(IReadOnlyList<object> history, PauseChoreCommand cmd)
    {
        switch (AdminAccess.Check(history, cmd.PinCode))
        {
            case AdminAccess.Result.NoHousehold: return Decision.Forbidden(HouseholdNotFound);
            case AdminAccess.Result.WrongPin: return Decision.Forbidden(AdminRequired);
        }

        // Normalize to whole days; pause windows are inclusive of both ends.
        var start = cmd.PauseStart.Date;
        var end = cmd.PauseEnd.Date;
        if (end < start) return Decision.Invalid(EndBeforeStart);

        var deletedChoreIds = history.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();
        var choreExists = history.OfType<ChoreCreated>()
            .Any(c => c.ChoreId == cmd.ChoreId && !deletedChoreIds.Contains(c.ChoreId));
        if (!choreExists) return Decision.Invalid(ChoreNotFound);

        var pauseId = cmd.PauseId == Guid.Empty ? Guid.NewGuid() : cmd.PauseId;

        return Decision.Accept(new ChorePaused(
            pauseId,
            cmd.ChoreId,
            cmd.HouseholdId,
            start,
            end,
            string.IsNullOrWhiteSpace(cmd.Reason) ? null : cmd.Reason.Trim()));
    }
}
