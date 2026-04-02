namespace ChoreMonkey.Events;

public record PaydayConfigured(Guid HouseholdId, int PaydayDayOfMonth, DateTime SetAt) : EventBase;
