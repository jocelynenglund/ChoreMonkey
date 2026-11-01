using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;

/// <summary>The MemberLookup view: the household's current members by id, with their latest nickname and status. Removed members are gone.</summary>
public static class MemberLookupView
{
    public static MemberLookupResponse Project(IReadOnlyList<object> history)
    {
        var members = new Dictionary<Guid, MemberDto>();

        foreach (var e in history)
        {
            switch (e)
            {
                case MemberJoinedHousehold joined:
                    members[joined.MemberId] = new(joined.MemberId, joined.Nickname, null);
                    break;
                case MemberNicknameChanged changed:
                    if (members.TryGetValue(changed.MemberId, out var m1))
                        members[changed.MemberId] = m1 with { Nickname = changed.NewNickname };
                    break;
                case MemberStatusChanged status:
                    if (members.TryGetValue(status.MemberId, out var m2))
                        members[status.MemberId] = m2 with { Status = status.Status };
                    break;
                case MemberRemoved removed:
                    members.Remove(removed.MemberId);
                    break;
            }
        }

        return new MemberLookupResponse(members);
    }
}
