using FileEventStore;

namespace ChoreMonkey.Events;

public record InviteGenerated(Guid HouseholdId, Guid InviteId, string Link) : EventBase;
