using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Quests;

public record GetQuestsQuery(Guid HouseholdId);

public record QuestDto(
    Guid ChoreId,
    string Name,
    string Description,
    string QuestType,       // "daily" | "weekly" | "boss" | "bonus"
    int XpReward,
    Guid[]? AssignedTo,
    bool AssignedToAll,
    bool CompletedToday,
    List<Guid> CompletedTodayBy);

public record GetQuestsResponse(List<QuestDto> Quests);

internal class Handler(IEventStore store)
{
    public async Task<GetQuestsResponse> HandleAsync(GetQuestsQuery request)
    {
        var history = (await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>().ToList();
        return QuestsView.Project(history, DateTime.UtcNow);
    }
}

internal static class QuestsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/quests",
            async (Guid householdId, Handler handler) =>
            {
                var result = await handler.HandleAsync(new GetQuestsQuery(householdId));
                return Results.Ok(result);
            });
    }
}
