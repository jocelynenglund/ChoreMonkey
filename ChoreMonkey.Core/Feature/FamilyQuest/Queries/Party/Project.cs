using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Party;

/// <summary>The party (each current member as a hero: XP, level, title, completions), folded from the events by the slice itself.</summary>
public static class PartyView
{
    private static readonly (int MinCompletions, string Title)[] Titles =
    [
        (0,   "Recruit"),
        (5,   "Apprentice"),
        (15,  "Dungeon Sweeper"),
        (30,  "Quest Runner"),
        (60,  "Dungeon Clearer"),
        (100, "Legendary Champion"),
    ];

    public static GetPartyResponse Project(IReadOnlyList<object> history, DateTime now)
    {
        var today     = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1);

        // Current members (joined minus removed), with latest nickname + status
        var removedIds = history.OfType<MemberRemoved>()
            .Select(e => e.MemberId)
            .ToHashSet();

        var memberData = new Dictionary<Guid, (string Nickname, string? Status)>();
        foreach (var e in history)
        {
            switch (e)
            {
                case MemberJoinedHousehold joined when !removedIds.Contains(joined.MemberId):
                    memberData[joined.MemberId] = (joined.Nickname, null);
                    break;
                case MemberNicknameChanged nc when memberData.ContainsKey(nc.MemberId):
                    memberData[nc.MemberId] = (nc.NewNickname, memberData[nc.MemberId].Status);
                    break;
                case MemberStatusChanged sc when memberData.ContainsKey(sc.MemberId):
                    memberData[sc.MemberId] = (memberData[sc.MemberId].Nickname, sc.Status);
                    break;
            }
        }

        foreach (var id in removedIds) memberData.Remove(id);

        // XP weights by chore frequency
        var choreXpMap = history.OfType<ChoreCreated>()
            .ToDictionary(e => e.ChoreId, e => XpForChore(e));

        var completions = history.OfType<ChoreCompleted>().ToList();

        var heroes = memberData.Select(kvp =>
        {
            var memberId  = kvp.Key;
            var nickname  = kvp.Value.Nickname;
            var status    = kvp.Value.Status;
            var myCompletions = completions.Where(c => c.CompletedByMemberId == memberId).ToList();
            var xp        = myCompletions.Sum(c => choreXpMap.GetValueOrDefault(c.ChoreId, 10));
            var level     = (int)Math.Sqrt(xp / 100.0);
            var total     = myCompletions.Count;
            var today_c   = myCompletions.Count(c => c.CompletedAt.Date == today);
            var week_c    = myCompletions.Count(c => c.CompletedAt.Date >= weekStart);
            var title     = Titles.LastOrDefault(t => total >= t.MinCompletions).Title ?? "Recruit";

            return new HeroDto(memberId, nickname, status, xp, level, title, total, today_c, week_c);
        }).ToList();

        return new GetPartyResponse(heroes);
    }

    private static int XpForChore(ChoreCreated e) => e.Frequency?.Type switch
    {
        "daily"    => 10,
        "weekly"   => 25,
        "interval" => 20,
        _          => e.IsOptional ? 15 : 50,
    };
}
