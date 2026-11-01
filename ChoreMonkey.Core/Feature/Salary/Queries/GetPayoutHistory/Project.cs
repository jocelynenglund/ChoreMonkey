using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetPayoutHistory;

/// <summary>The PayoutHistory view: every closed period, newest first, with each member's payout. The handler is only the store adapter.</summary>
public static class PayoutHistoryView
{
    public static PayoutHistoryResponse Project(IReadOnlyList<object> history)
    {
        var periods = history
            .OfType<PeriodClosed>()
            .OrderByDescending(e => e.PeriodEnd)
            .Select(e => new PeriodSummaryDto(
                e.PeriodId,
                e.PeriodStart,
                e.PeriodEnd,
                e.Payouts.Select(p => new PayoutSummaryDto(
                    p.MemberId,
                    p.Name,
                    p.BaseSalary,
                    p.GrossDeductions,
                    p.GrossBonuses,
                    p.NetPay)).ToList()))
            .ToList();

        return new PayoutHistoryResponse(periods);
    }
}
