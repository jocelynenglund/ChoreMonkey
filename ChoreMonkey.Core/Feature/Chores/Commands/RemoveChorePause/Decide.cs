using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.RemoveChorePause;

/// <summary>
/// Remove Chore Pause: admin PIN needed (Forbidden); only an active (created, not yet removed) pause of that chore can be
/// removed (NotFound). History is the household's events and the chores' events together.
/// </summary>
public static class RemoveChorePauseSlice
{
    public const string AdminRequired = "Admin access required";
    public const string NotFound = "Pause not found";

    public static Decision Decide(IReadOnlyList<object> history, RemoveChorePauseCommand cmd)
    {
        if (AdminAccess.Check(history, cmd.PinCode) != AdminAccess.Result.Admin) return Decision.Forbidden(AdminRequired);

        var pauses = ChorePauses.Build(history);
        var isActive = pauses.TryGetValue(cmd.ChoreId, out var windows)
            && windows.Any(w => w.PauseId == cmd.PauseId);
        if (!isActive) return Decision.NotFound(NotFound);

        return Decision.Accept(new ChorePauseRemoved(cmd.PauseId, cmd.ChoreId, cmd.HouseholdId));
    }
}
