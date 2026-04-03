using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Domain;

internal class ChoreAggregate
{
    public static string StreamId(Guid householdId) => $"chores-{householdId}";

    /// <summary>
    /// Builds the current state of each chore by replaying ChoreCreated + ChoreUpdated events.
    /// Returns only non-deleted chores, keyed by ChoreId.
    /// </summary>
    public static Dictionary<Guid, ChoreCreated> BuildChores(IReadOnlyList<object> choreEvents)
    {
        var deletedIds = choreEvents.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();

        // Start with created chores
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(c => !deletedIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);

        // Apply updates — project ChoreUpdated onto the ChoreCreated shape
        foreach (var update in choreEvents.OfType<ChoreUpdated>())
        {
            if (!chores.ContainsKey(update.ChoreId)) continue;
            chores[update.ChoreId] = chores[update.ChoreId] with
            {
                DisplayName = update.DisplayName,
                Description = update.Description,
                Frequency = update.Frequency,
                IsOptional = update.IsOptional,
                StartDate = update.StartDate,
                IsRequired = update.IsRequired,
                MissedDeduction = update.MissedDeduction,
            };
        }

        return chores;
    }
}
