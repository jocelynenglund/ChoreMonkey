using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Household.Queries.AccessHousehold;

/// <summary>
/// The AccessHousehold view: whether a PIN opens the household, and as admin or as member. The admin hash is
/// tried first, then the member hash if there is one. The PIN check itself (PBKDF2, slow and randomized) is
/// handed in as <paramref name="pinMatchesHash"/> by the adapter; this view only decides which hashes to try
/// and in what order.
/// </summary>
public static class AccessHouseholdView
{
    public static AccessHouseholdResponse Project(IReadOnlyList<object> history, Guid householdId, Func<string, bool> pinMatchesHash)
    {
        var householdCreated = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
            return new AccessHouseholdResponse(false, householdId, null);

        var currentAdminPinHash = history.OfType<AdminPinChanged>().LastOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        if (pinMatchesHash(currentAdminPinHash))
            return new AccessHouseholdResponse(true, householdId, householdCreated.Name, IsAdmin: true);

        // The member PIN may be from creation or a later change.
        var currentMemberPinHash = history.OfType<MemberPinChanged>().LastOrDefault()?.NewMemberPinHash ?? householdCreated.MemberPinHash;
        if (currentMemberPinHash != null && pinMatchesHash(currentMemberPinHash))
            return new AccessHouseholdResponse(true, householdId, householdCreated.Name, IsAdmin: false);

        return new AccessHouseholdResponse(false, householdId, null);
    }
}
