using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetAvailablePeriods;

/// <summary>
/// The AvailablePeriods view: the pay period in progress (when it ends in the future) followed by up to 24 completed
/// periods back, never before the household existed, each marked closed or not. Folded from the household and salary
/// events by the slice itself. The handler is only the store adapter.
/// </summary>
public static class AvailablePeriodsView
{
    public static AvailablePeriodsResponse Project(IReadOnlyList<object> history, DateTime now)
    {
        var today = now.Date;

        // Household creation date — don't generate periods before this
        var created = history.OfType<HouseholdCreated>().FirstOrDefault();
        var householdCreatedAt = created is not null && DateTime.TryParse(created.TimestampUtc, out var parsedTs)
            ? (DateTime?)parsedTs.Date.ToUniversalTime()
            : null;

        // Payday config
        var paydayDay = SalaryFolds.Payday(history);

        // Already closed periods by their (start, end) for lookup
        // Use last-wins if same period was somehow closed twice
        var closedPeriods = history
            .OfType<PeriodClosed>()
            .GroupBy(e => (e.PeriodStart.Date, e.PeriodEnd.Date))
            .ToDictionary(g => g.Key, g => g.Last().PeriodId);

        var periods = new List<AvailablePeriodDto>();

        // Prepend the in-progress period — boundaries must match GetCurrentPeriod so the
        // frontend can recognize it as "current" and offer previews.
        var paydayThisMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        DateTime currentStart, currentEnd;
        if (today <= paydayThisMonth.Date)
        {
            var prevPayday = paydayThisMonth.AddMonths(-1);
            currentStart = new DateTime(prevPayday.Year, prevPayday.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            currentEnd = paydayThisMonth;
        }
        else
        {
            var nextPayday = paydayThisMonth.AddMonths(1);
            currentStart = paydayThisMonth.AddDays(1);
            currentEnd = new DateTime(nextPayday.Year, nextPayday.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        }

        // Only prepend if the period is still in progress (ends in the future). When
        // today == payday, the backwards loop below will emit the same period as a
        // completed-but-unclosed entry — skipping here avoids duplicates.
        if (currentEnd > today && (!householdCreatedAt.HasValue || currentEnd >= householdCreatedAt.Value))
        {
            periods.Add(new AvailablePeriodDto(currentStart, currentEnd, false, null));
        }

        // Walk backwards generating completed period boundaries
        var cursor = GetPaydayOn(today, paydayDay);
        for (var i = 0; i < 24; i++) // max 24 months back
        {
            var periodEnd = cursor;
            var prevPayday = cursor.AddMonths(-1);
            var periodStart = new DateTime(prevPayday.Year, prevPayday.Month, paydayDay,
                0, 0, 0, DateTimeKind.Utc).AddDays(1);

            // Stop before household existed
            if (householdCreatedAt.HasValue && periodEnd < householdCreatedAt.Value)
                break;

            if (periodEnd <= today)
            {
                var isClosed = closedPeriods.TryGetValue((periodStart.Date, periodEnd.Date), out var periodId);
                periods.Add(new AvailablePeriodDto(periodStart, periodEnd, isClosed, isClosed ? periodId : null));
            }

            cursor = new DateTime(prevPayday.Year, prevPayday.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);

            if (householdCreatedAt.HasValue && cursor < householdCreatedAt.Value)
                break;
        }

        return new AvailablePeriodsResponse(periods);
    }

    private static DateTime GetPaydayOn(DateTime date, int paydayDay)
    {
        var paydayThisMonth = new DateTime(date.Year, date.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        // If today is past payday this month, most recent completed period ended this month's payday
        return date >= paydayThisMonth.Date ? paydayThisMonth : paydayThisMonth.AddMonths(-1);
    }
}
