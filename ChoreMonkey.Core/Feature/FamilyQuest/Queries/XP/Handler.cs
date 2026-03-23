using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.XP;

public record GetXpQuery(Guid HouseholdId);

public record RewardTierDto(int XpRequired, string Reward, string Emoji);

public record GetXpResponse(
    int TotalXp,
    RewardTierDto? CurrentReward,
    RewardTierDto? NextReward,
    int XpToNext,
    int ProgressPercent);

internal class Handler(IEventStore store)
{
    private static readonly RewardTierDto[] Tiers =
    [
        new(500,   "Pizza Night",    "🍕"),
        new(1500,  "Movie Night",    "🎬"),
        new(3000,  "Dinner Out",     "🍽️"),
        new(6000,  "Day Trip",       "🎢"),
        new(15000, "Weekend Away",   "🏖️"),
        new(50000, "The Big One™",   "✈️"),
    ];

    public async Task<GetXpResponse> HandleAsync(GetXpQuery request)
    {
        var choreEvents = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var choreXpMap = choreEvents.OfType<ChoreCreated>()
            .ToDictionary(e => e.ChoreId, e => XpForChore(e));

        var totalXp = choreEvents.OfType<ChoreCompleted>()
            .Sum(c => choreXpMap.GetValueOrDefault(c.ChoreId, 10));

        var currentReward = Tiers.LastOrDefault(t => totalXp >= t.XpRequired);
        var nextReward    = Tiers.FirstOrDefault(t => totalXp < t.XpRequired);

        int xpToNext       = nextReward is null ? 0 : nextReward.XpRequired - totalXp;
        int tierStart      = currentReward?.XpRequired ?? 0;
        int tierEnd        = nextReward?.XpRequired ?? Tiers.Last().XpRequired;
        int progressPct    = tierEnd == tierStart
            ? 100
            : (int)((totalXp - tierStart) / (double)(tierEnd - tierStart) * 100);

        return new GetXpResponse(totalXp, currentReward, nextReward, xpToNext, progressPct);
    }

    private static int XpForChore(ChoreCreated e) => e.Frequency?.Type switch
    {
        "daily"    => 10,
        "weekly"   => 25,
        "interval" => 20,
        _          => e.IsOptional ? 15 : 50,
    };
}

internal static class XpEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/xp",
            async (Guid householdId, Handler handler) =>
            {
                var result = await handler.HandleAsync(new GetXpQuery(householdId));
                return Results.Ok(result);
            });
    }
}
