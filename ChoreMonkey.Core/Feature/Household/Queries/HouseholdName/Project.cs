using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Household.Queries.HouseholdName;

/// <summary>The HouseholdName view: the household's name ("Unknown" before it exists) and its latest slug.</summary>
public static class HouseholdNameView
{
    public static HouseholdNameResponse Project(IReadOnlyList<object> history, Guid householdId)
    {
        var householdCreated = history.OfType<HouseholdCreated>().FirstOrDefault();
        var slug = history.OfType<HouseholdSlugSet>().LastOrDefault()?.Slug;

        return new HouseholdNameResponse(householdId, householdCreated?.Name ?? "Unknown", slug);
    }
}
