using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Quests;

/// <summary>The quests (every live chore as a quest, with today's completions), folded from the events by the slice itself.</summary>
public static class QuestsView
{
    public static GetQuestsResponse Project(IReadOnlyList<object> history, DateTime now)
    {
        var today = now.Date;

        var deletedIds  = history.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();
        var assignments = history.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        var completions = history.OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt.Date == today)
            .GroupBy(c => c.ChoreId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.CompletedByMemberId).ToList());

        var quests = history.OfType<ChoreCreated>()
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
