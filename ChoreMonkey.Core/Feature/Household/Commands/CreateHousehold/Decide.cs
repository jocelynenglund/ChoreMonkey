using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Household.Commands.CreateHousehold;

/// <summary>
/// Create Household: the household is created and its owner joins as the first member.
/// The PINs arrive already hashed (hashing is randomized and slow, so the adapter does it), and so does
/// the new member's id. Nothing is checked here; the store refuses a stream that already exists.
/// </summary>
public static class CreateHouseholdSlice
{
    public static Decision Decide(
        IReadOnlyList<object> history,
        CreateHouseholdCommand cmd,
        string adminPinHash,
        string? memberPinHash,
        Guid memberId) =>
        Decision.Accept(
            new HouseholdCreated(cmd.HouseholdId, cmd.Name, adminPinHash, memberPinHash),
            new MemberJoinedHousehold(memberId, cmd.HouseholdId, Guid.Empty, cmd.OwnerNickname));
}
