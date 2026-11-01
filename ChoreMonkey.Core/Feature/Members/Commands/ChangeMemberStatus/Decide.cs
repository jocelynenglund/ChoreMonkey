using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberStatus;

/// <summary>
/// Change Member Status: the member must have joined (NotFound; a removed member still counts). The status is
/// recorded as given, even when it is the same as before.
/// </summary>
public static class ChangeMemberStatusSlice
{
    public const string MemberNotFound = "Member not found";

    public static Decision Decide(IReadOnlyList<object> history, ChangeMemberStatusCommand cmd)
    {
        if (!history.OfType<MemberJoinedHousehold>().Any(m => m.MemberId == cmd.MemberId))
            return Decision.NotFound(MemberNotFound);

        return Decision.Accept(new MemberStatusChanged(cmd.MemberId, cmd.HouseholdId, cmd.Status));
    }
}
