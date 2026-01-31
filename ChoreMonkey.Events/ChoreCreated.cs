namespace ChoreMonkey.Events;
public record ChoreCreated(Guid ChoreId, Guid HouseholdId, string DisplayName, string Description) : EventBase;
