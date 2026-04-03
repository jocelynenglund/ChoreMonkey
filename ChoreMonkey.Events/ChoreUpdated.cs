namespace ChoreMonkey.Events;

public record ChoreUpdated(
    Guid ChoreId,
    Guid HouseholdId,
    string DisplayName,
    string Description,
    ChoreFrequency? Frequency,
    bool IsOptional,
    DateTime? StartDate,
    bool IsRequired,
    decimal MissedDeduction) : EventBase;
