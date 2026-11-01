using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Members.Commands.JoinHousehold;

/// <summary>
/// Join Household: the invite must have been generated for this household (Invalid otherwise). An invite can
/// be used again and again, and the nickname isn't checked. The adapter supplies the new member's id.
/// </summary>
public static class JoinHouseholdSlice
{
    public const string InvalidInvite = "Invalid invite";

    public static Decision Decide(IReadOnlyList<object> history, JoinHouseholdCommand cmd, Guid memberId)
    {
        var invite = history.OfType<InviteGenerated>().FirstOrDefault(e => e.InviteId == cmd.InviteId);
        if (invite == null) return Decision.Invalid(InvalidInvite);

        return Decision.Accept(new MemberJoinedHousehold(memberId, cmd.HouseholdId, cmd.InviteId, cmd.Nickname));
    }
}
