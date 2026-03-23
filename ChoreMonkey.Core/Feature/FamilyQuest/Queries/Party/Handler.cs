using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Party;

public record GetPartyQuery(Guid HouseholdId);

public record HeroDto(
    Guid MemberId,
    string Nickname,
    int Xp,
    int Level,
    string Title,
    int TotalCompletions,
    int CompletionsToday,
    int CompletionsThisWeek);

public record GetPartyResponse(List<HeroDto> Heroes);

internal class Handler(IEventStore store)
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

    public async Task<GetPartyResponse> HandleAsync(GetPartyQuery request)
    {
        var householdEvents = await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId));
        var choreEvents     = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var today     = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1);

        // Current members (joined minus removed)
        var removedIds = householdEvents.OfType<MemberRemoved>()
            .Select(e => e.MemberId)
            .ToHashSet();

        var members = householdEvents.OfType<MemberJoinedHousehold>()
            .Where(e => !removedIds.Contains(e.MemberId))
            .GroupBy(e => e.MemberId)
            .Select(g => g.Last())
            .ToDictionary(e => e.MemberId, e => e.Nickname);

        // Apply nickname changes
        foreach (var nc in householdEvents.OfType<MemberNicknameChanged>())
            if (members.ContainsKey(nc.MemberId))
                members[nc.MemberId] = nc.NewNickname;

        // XP weights by chore frequency
        var choreXpMap = choreEvents.OfType<ChoreCreated>()
            .ToDictionary(e => e.ChoreId, e => XpForChore(e));

        var completions = choreEvents.OfType<ChoreCompleted>().ToList();

        var heroes = members.Select(kvp =>
        {
            var memberId   = kvp.Key;
            var nickname   = kvp.Value;
            var myCompletions = completions.Where(c => c.CompletedByMemberId == memberId).ToList();
            var xp         = myCompletions.Sum(c => choreXpMap.GetValueOrDefault(c.ChoreId, 10));
            var level      = (int)Math.Sqrt(xp / 100.0);
            var total      = myCompletions.Count;
            var today_c    = myCompletions.Count(c => c.CompletedAt.Date == today);
            var week_c     = myCompletions.Count(c => c.CompletedAt.Date >= weekStart);
            var title      = Titles.LastOrDefault(t => total >= t.MinCompletions).Title ?? "Recruit";

            return new HeroDto(memberId, nickname, xp, level, title, total, today_c, week_c);
        }).ToList();

        return new GetPartyResponse(heroes);
    }

    private static int XpForChore(ChoreCreated e) => e.Frequency?.Type switch
    {
        "daily"    => 10,
        "weekly"   => 25,
        "interval" => 20,
        _          => e.IsOptional ? 15 : 50, // once / boss quest
    };
}

internal static class PartyEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/party",
            async (Guid householdId, Handler handler) =>
            {
                var result = await handler.HandleAsync(new GetPartyQuery(householdId));
                return Results.Ok(result);
            });
    }
}
