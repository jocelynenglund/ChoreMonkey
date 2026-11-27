namespace ChoreMonkey.Events;

public record DisplayLinkRevoked(Guid HouseholdId, Guid LinkId) : EventBase;
