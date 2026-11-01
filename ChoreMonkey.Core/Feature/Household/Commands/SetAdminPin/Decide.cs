using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetAdminPin;

/// <summary>
/// Set Admin Pin: the household must exist and the current admin PIN must be right (Forbidden otherwise).
/// The adapter verifies the current PIN against the stored hash and hashes the new one; it hands over the
/// verdict and the hash (null when the current PIN was wrong).
/// </summary>
public static class SetAdminPinSlice
{
    public const string HouseholdNotFound = "Household not found";
    public const string AdminRequired = "Admin access required";

    public static Decision Decide(IReadOnlyList<object> history, SetAdminPinCommand cmd, bool currentPinVerified, string? newPinHash)
    {
        if (!history.OfType<HouseholdCreated>().Any()) return Decision.Forbidden(HouseholdNotFound);
        if (!currentPinVerified || newPinHash == null) return Decision.Forbidden(AdminRequired);

        return Decision.Accept(new AdminPinChanged(cmd.HouseholdId, newPinHash));
    }
}
