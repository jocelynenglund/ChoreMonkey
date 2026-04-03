using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetAvailablePeriods;

public record GetAvailablePeriodsQuery(Guid HouseholdId);

public record AvailablePeriodDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool IsClosed,
    Guid? PeriodId);       // null if not yet closed

public record AvailablePeriodsResponse(List<AvailablePeriodDto> Periods);

internal class Handler(IEventStore store)
{
    public async Task<AvailablePeriodsResponse> HandleAsync(GetAvailablePeriodsQuery request)
    {
        var today = DateTime.UtcNow.Date;

        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var salaryEvents = await store.FetchEventsAsync(salaryStreamId);

        // Household creation date — don't generate periods before this
        var created = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        var householdCreatedAt = created is not null && DateTime.TryParse(created.TimestampUtc, out var parsedTs)
            ? (DateTime?)parsedTs.Date.ToUniversalTime()
            : null;

        // Payday config
        var paydayDay = salaryEvents
            .OfType<PaydayConfigured>()
            .LastOrDefault()?.PaydayDayOfMonth ?? 25;

        // Already closed periods by their (start, end) for lookup
        var closedPeriods = salaryEvents
            .OfType<PeriodClosed>()
            .ToDictionary(
                e => (e.PeriodStart.Date, e.PeriodEnd.Date),
                e => e.PeriodId);

        // Generate all completed pay periods from household creation up to today
        var periods = new List<AvailablePeriodDto>();
        var cursor = GetPaydayOn(today, paydayDay);

        // Walk backwards, generating period boundaries
        for (var i = 0; i < 24; i++) // max 24 months back
        {
            var periodEnd = cursor;
            var prevPayday = cursor.AddMonths(-1);
            var periodStart = new DateTime(prevPayday.Year, prevPayday.Month, paydayDay,
                0, 0, 0, DateTimeKind.Utc).AddDays(1);

            // Stop before household existed
            if (householdCreatedAt.HasValue && periodEnd < householdCreatedAt.Value)
                break;

            // Only include completed periods (periodEnd <= today)
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

internal static class GetAvailablePeriodsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/available-periods", async (
            Guid householdId,
            Handler handler) =>
        {
            var query = new GetAvailablePeriodsQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
