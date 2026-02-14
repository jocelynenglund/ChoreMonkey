namespace ChoreMonkey.Events;

public record ChoreCreated(
    Guid ChoreId, 
    Guid HouseholdId, 
    string DisplayName, 
    string Description,
    ChoreFrequency? Frequency = null,
    bool IsOptional = false) : EventBase;

public record ChoreFrequency(
    string Type,           // "daily", "weekly", "interval", "once"
    string[]? Days = null, // For weekly: ["monday", "thursday"]
    int? IntervalDays = null); // For interval: every X days
