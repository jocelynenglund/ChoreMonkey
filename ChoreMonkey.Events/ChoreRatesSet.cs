namespace ChoreMonkey.Events;

public record ChoreRatesSet(
    Guid HouseholdId,
    Guid ChoreId,
    decimal? DeductionRate,
    decimal? BonusRate,
    DateTime SetAt) : EventBase;
