using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Members.Commands.RemoveMember;

/// <summary>
/// Remove Member: the household must exist, the admin PIN must be right (Forbidden), the member must exist and
/// not be removed already, and nobody removes themselves. The adapter verifies the PIN (an unparsable PIN is
/// simply not verified). The removal records the member's current nickname.
/// </summary>
public static class RemoveMemberSlice
{
    public const string HouseholdNotFound = "Household not found";
    public const string InvalidPin = "Invalid admin PIN";
    public const string MemberNotFound = "Member not found";
    public const string AlreadyRemoved = "Member already removed";
    public const string CannotRemoveYourself = "Cannot remove yourself";

    public static Decision Decide(IReadOnlyList<object> history, RemoveMemberCommand cmd, bool adminPinVerified)
    {
        if (!history.OfType<HouseholdCreated>().Any()) return Decision.NotFound(HouseholdNotFound);
        if (!adminPinVerified) return Decision.Forbidden(InvalidPin);

        var memberJoined = history.OfType<MemberJoinedHousehold>().FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (memberJoined == null) return Decision.NotFound(MemberNotFound);

        if (history.OfType<MemberRemoved>().Any(m => m.MemberId == cmd.MemberId)) return Decision.Invalid(AlreadyRemoved);

        if (cmd.MemberId == cmd.RemovedByMemberId) return Decision.Invalid(CannotRemoveYourself);

        var nickname = history.OfType<MemberNicknameChanged>()
            .Where(n => n.MemberId == cmd.MemberId)
            .LastOrDefault()?.NewNickname ?? memberJoined.Nickname;

        return Decision.Accept(new MemberRemoved(cmd.HouseholdId, cmd.MemberId, cmd.RemovedByMemberId, nickname));
    }
}
