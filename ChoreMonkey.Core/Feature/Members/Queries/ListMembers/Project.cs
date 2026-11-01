using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;

namespace ChoreMonkey.Core.Feature.Members.Queries.ListMembers;

/// <summary>The ListMembers view: the household's current members as the API lists them.</summary>
public static class ListMembersView
{
    public static ListMembersResponse Project(IReadOnlyList<object> history)
    {
        var lookup = MemberLookupView.Project(history);

        var members = lookup.Members.Values
            .Select(m => new MemberResponseDto(m.Id, m.Nickname, m.Status))
            .ToList();

        return new ListMembersResponse(members);
    }
}
