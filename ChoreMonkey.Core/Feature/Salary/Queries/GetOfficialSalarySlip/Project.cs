using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetOfficialSalarySlip;

/// <summary>The OfficialSalarySlip view: one member's slip for one closed period, or nothing when the period or the member's payout isn't there. The handler is only the store adapter.</summary>
public static class OfficialSalarySlipView
{
    public static OfficialSalarySlipResponse? Project(IReadOnlyList<object> history, Guid periodId, Guid memberId)
    {
        var period = history
            .OfType<PeriodClosed>()
            .FirstOrDefault(e => e.PeriodId == periodId);

        if (period is null)
            return null;

        var payout = period.Payouts
            .FirstOrDefault(p => p.MemberId == memberId);

        if (payout is null)
            return null;

        return new OfficialSalarySlipResponse(
            period.PeriodId,
            period.PeriodStart,
            period.PeriodEnd,
            payout.Name,
            payout.BaseSalary,
            payout.Deductions.Select(d => new DeductionLineDto(
                d.ChoreName,
                d.BaseRate,
                d.Multiplier,
                d.Amount)).ToList(),
            payout.Bonuses.Select(b => new BonusLineDto(
                b.ChoreName,
                b.BaseRate,
                b.Multiplier,
                b.Amount)).ToList(),
            payout.GrossDeductions,
            payout.GrossBonuses,
            payout.NetPay);
    }
}
