namespace ChoreMonkey.Events;

public record ChoreAssigned(
    Guid ChoreId,
    Guid HouseholdId,
    Guid? AssignedToMemberId
) : EventBase;
