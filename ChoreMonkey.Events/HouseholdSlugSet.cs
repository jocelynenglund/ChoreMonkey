namespace ChoreMonkey.Events;

public record HouseholdSlugSet(Guid HouseholdId, string Slug, DateTime SetAt) : EventBase;
