using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.DeleteChore;

/// <summary>
/// Delete Chore: admin PIN needed (no household or a wrong PIN is Forbidden), the chore must exist (NotFound),
/// and deleting a deleted chore records nothing. History is the household's events and the chores' events together.
/// </summary>
public static class DeleteChoreSlice
{
    public const string AdminRequired = "Admin access required";
    public const string NotFound = "Chore not found";

    public static Decision Decide(IReadOnlyList<object> history, DeleteChoreCommand cmd)
    {
        if (AdminAccess.Check(history, cmd.PinCode) != AdminAccess.Result.Admin) return Decision.Forbidden(AdminRequired);

        if (!history.OfType<ChoreCreated>().Any(c => c.ChoreId == cmd.ChoreId)) return Decision.NotFound(NotFound);

        if (history.OfType<ChoreDeleted>().Any(c => c.ChoreId == cmd.ChoreId)) return Decision.Nothing;

        return Decision.Accept(new ChoreDeleted(cmd.ChoreId, cmd.HouseholdId, Guid.Empty));
    }
}
