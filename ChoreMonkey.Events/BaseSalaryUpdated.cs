namespace ChoreMonkey.Events;

public record BaseSalaryUpdated(
    Guid HouseholdId, 
    decimal NewBaseAmount) : EventBase;
