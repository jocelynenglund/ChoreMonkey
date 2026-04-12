namespace ChoreMonkey.Events;

public record PaydayConfigured(Guid HouseholdId, int PaydayDayOfMonth) : EventBase;
