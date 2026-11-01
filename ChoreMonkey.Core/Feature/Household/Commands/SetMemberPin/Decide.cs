using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetMemberPin;

/// <summary>
/// Set Member Pin: the household must exist and the admin PIN must be right (Forbidden otherwise).
/// The adapter verifies the admin PIN and hashes the new member PIN; it hands over the verdict and the
/// hash (null when the admin PIN was wrong).
/// </summary>
public static class SetMemberPinSlice
{
    public const string HouseholdNotFound = "Household not found";
    public const string AdminRequired = "Admin access required";

    public static Decision Decide(IReadOnlyList<object> history, SetMemberPinCommand cmd, bool adminPinVerified, string? memberPinHash)
    {
        if (!history.OfType<HouseholdCreated>().Any()) return Decision.Forbidden(HouseholdNotFound);
        if (!adminPinVerified || memberPinHash == null) return Decision.Forbidden(AdminRequired);

        return Decision.Accept(new MemberPinChanged(cmd.HouseholdId, memberPinHash));
    }
}
