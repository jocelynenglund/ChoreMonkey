using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberNickname;

/// <summary>
/// Change Member Nickname: the member must have joined (NotFound; a removed member still counts). The change
/// records the old nickname (the latest change by time, else the joining one); asking for the nickname the
/// member already has records nothing.
/// </summary>
public static class ChangeMemberNicknameSlice
{
    public const string MemberNotFound = "Member not found";

    public static Decision Decide(IReadOnlyList<object> history, ChangeMemberNicknameCommand cmd)
    {
        var memberJoined = history.OfType<MemberJoinedHousehold>().FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (memberJoined == null) return Decision.NotFound(MemberNotFound);

        var currentNickname = history.OfType<MemberNicknameChanged>()
            .Where(m => m.MemberId == cmd.MemberId)
            .OrderByDescending(m => m.TimestampUtc)
            .Select(m => m.NewNickname)
            .FirstOrDefault() ?? memberJoined.Nickname;

        if (currentNickname == cmd.NewNickname) return Decision.Nothing;

        return Decision.Accept(new MemberNicknameChanged(cmd.MemberId, cmd.HouseholdId, currentNickname, cmd.NewNickname));
    }
}
