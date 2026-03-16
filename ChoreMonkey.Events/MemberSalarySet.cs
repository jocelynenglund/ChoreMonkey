namespace ChoreMonkey.Events;

public record MemberSalarySet(
    Guid HouseholdId,
    Guid MemberId,
    decimal BaseSalary,
    decimal DeductionMultiplier,
    decimal BonusMultiplier,
    DateTime SetAt) : EventBase;
