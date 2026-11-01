using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Household.Queries.GetHouseholdBySlug;

/// <summary>
/// The GetHouseholdBySlug view: a slug's household with its name and active member count, or null when the
/// slug isn't claimed or its household has no creation event. History is the slug's own stream followed by
/// the claiming household's events.
/// </summary>
public static class GetHouseholdBySlugView
{
    public static GetHouseholdBySlugResponse? Project(IReadOnlyList<object> history)
    {
        var claimed = history.OfType<SlugClaimed>().FirstOrDefault();
        if (claimed == null)
            return null;

        var householdCreated = history.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
            return null;

        var memberCount = history.OfType<MemberJoinedHousehold>().Select(e => e.MemberId).ToHashSet();
        var removedMembers = history.OfType<MemberRemoved>().Select(e => e.MemberId).ToHashSet();
        var activeMemberCount = memberCount.Except(removedMembers).Count();

        return new GetHouseholdBySlugResponse(claimed.HouseholdId, householdCreated.Name, activeMemberCount);
    }
}
