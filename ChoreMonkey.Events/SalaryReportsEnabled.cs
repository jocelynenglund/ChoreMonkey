namespace ChoreMonkey.Events;

public record SalaryReportsEnabled(
    Guid HouseholdId, 
    decimal BaseAmount = 800m) : EventBase;
