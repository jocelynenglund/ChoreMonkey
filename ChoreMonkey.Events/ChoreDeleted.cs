namespace ChoreMonkey.Events;

public record ChoreDeleted(Guid ChoreId, Guid HouseholdId, Guid DeletedByMemberId) : EventBase;
