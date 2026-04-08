namespace ChoreMonkey.Events;

public record SlugClaimed(Guid HouseholdId, string Slug, DateTime ClaimedAt) : EventBase;
