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
        var events = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));
        var today  = DateTime.UtcNow.Date;

        var deletedIds  = events.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();
        var assignments = events.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        var completions = events.OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt.Date == today)
            .GroupBy(c => c.ChoreId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.CompletedByMemberId).ToList());

        var quests = events.OfType<ChoreCreated>()
            .Where(e => !deletedIds.Contains(e.ChoreId))
            .Select(e =>
            {
                var assignment    = assignments.GetValueOrDefault(e.ChoreId);
                var todayCompletions = completions.GetValueOrDefault(e.ChoreId) ?? [];
                var questType     = QuestType(e);
                var xp            = XpReward(e);
                var completedToday = todayCompletions.Count > 0;

                return new QuestDto(
                    e.ChoreId,
                    e.DisplayName,
                    e.Description,
                    questType,
                    xp,
                    assignment?.AssignedToMemberIds,
                    assignment?.AssignToAll ?? false,
                    completedToday,
                    todayCompletions);
            })
            .ToList();

        return new GetQuestsResponse(quests);
    }

    private static string QuestType(ChoreCreated e) => e.Frequency?.Type switch
    {
        "daily"    => "daily",
        "weekly"   => "weekly",
        "interval" => "weekly",
        _          => e.IsOptional ? "bonus" : "boss",
    };

    private static int XpReward(ChoreCreated e) => e.Frequency?.Type switch
    {
        "daily"    => 10,
        "weekly"   => 25,
        "interval" => 20,
        _          => e.IsOptional ? 15 : 50,
    };
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
