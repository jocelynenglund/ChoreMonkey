using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.DisplayLinks;

/// <summary>The household's active members (joined, not removed) with their latest nicknames, in the order they joined.</summary>
internal static class DisplayMembers
{
    public static List<(Guid MemberId, string Nickname)> Active(IReadOnlyList<object> history)
    {
        var removed = history.OfType<MemberRemoved>().Select(e => e.MemberId).ToHashSet();
        var names = new Dictionary<Guid, string>();
        var order = new List<Guid>();
        foreach (var e in history)
        {
            if (e is MemberJoinedHousehold j && !names.ContainsKey(j.MemberId))
            {
                names[j.MemberId] = j.Nickname;
                order.Add(j.MemberId);
            }
            else if (e is MemberNicknameChanged n)
            {
                names[n.MemberId] = n.NewNickname;
            }
        }
        return order.Where(id => !removed.Contains(id)).Select(id => (id, names[id])).ToList();
    }
}
