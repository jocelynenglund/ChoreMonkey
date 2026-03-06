namespace ChoreMonkey.Events;

public record SalaryReportGenerated(
    Guid HouseholdId,
    Guid MemberId,
    string Period,              // e.g., "2026-W10" or "2026-03"
    decimal BaseAmount,
    SalaryDeduction[] Deductions,
    decimal FinalAmount) : EventBase;

public record SalaryDeduction(
    Guid ChoreId,
    string ChoreName,
    string MissedPeriod,        // e.g., "2026-03-05" for daily, "2026-W09" for weekly
    decimal Amount);
