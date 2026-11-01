using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.XP;

/// <summary>The family's XP and reward tiers, folded from the chore events by the slice itself.</summary>
public static class XpView
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

    public static GetXpResponse Project(IReadOnlyList<object> history)
    {
        var choreXpMap = history.OfType<ChoreCreated>()
            .ToDictionary(e => e.ChoreId, e => XpForChore(e));

        var totalXp = history.OfType<ChoreCompleted>()
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
