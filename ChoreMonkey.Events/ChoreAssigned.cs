namespace ChoreMonkey.Events;

public record ChoreAssigned(
    Guid ChoreId,
    Guid HouseholdId,
    Guid[]? AssignedToMemberIds = null,
    bool AssignToAll = false
) : EventBase;
