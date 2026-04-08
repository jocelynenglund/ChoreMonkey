namespace ChoreMonkey.Events;

public record PeriodClosed(
    Guid PeriodId,
    Guid HouseholdId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyList<PeriodPayout> Payouts,
    DateTime ClosedAt) : EventBase;

public record PeriodPayout(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal GrossDeductions,
    decimal GrossBonuses,
    decimal NetPay,
    IReadOnlyList<PayoutDeduction> Deductions,
    IReadOnlyList<PayoutBonus> Bonuses);

public record PayoutDeduction(
    Guid ChoreId,
    string ChoreName,
    decimal BaseRate,
    decimal Multiplier,
    decimal Amount);

public record PayoutBonus(
    Guid ChoreId,
    string ChoreName,
    decimal BaseRate,
    decimal Multiplier,
    decimal Amount);
