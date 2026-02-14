namespace ChoreMonkey.Events;

public record ChoreCompleted(
    Guid ChoreId, 
    Guid HouseholdId,
    Guid CompletedByMemberId, 
    DateTime CompletedAt) : EventBase;
