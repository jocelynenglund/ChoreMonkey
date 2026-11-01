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
    public async Task<GetXpResponse> HandleAsync(GetXpQuery request)
    {
        var history = (await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>().ToList();
        return XpView.Project(history);
    }
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
