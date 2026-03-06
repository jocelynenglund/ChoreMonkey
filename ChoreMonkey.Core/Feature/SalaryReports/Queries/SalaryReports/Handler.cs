using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.SalaryReports.Queries.SalaryReports;

public record GetSalaryReportsQuery(Guid HouseholdId, Guid? MemberId = null);

public record SalaryReportDto(
    string Period,
    Guid MemberId,
    decimal BaseAmount,
    List<DeductionDto> Deductions,
    decimal FinalAmount,
    string GeneratedAt);

public record DeductionDto(Guid ChoreId, string ChoreName, string MissedPeriod, decimal Amount);

public record SalaryReportsResponse(List<SalaryReportDto> Reports);

internal class Handler(IEventStore store)
{
    public async Task<SalaryReportsResponse> HandleAsync(GetSalaryReportsQuery request)
    {
        var streamId = SalaryReportsAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        var reportEvents = events.OfType<SalaryReportGenerated>();
        
        if (request.MemberId.HasValue)
        {
            reportEvents = reportEvents.Where(e => e.MemberId == request.MemberId.Value);
        }
        
        var reports = reportEvents
            .OrderByDescending(e => e.Period)
            .ThenBy(e => e.TimestampUtc)
            .Select(e => new SalaryReportDto(
                e.Period,
                e.MemberId,
                e.BaseAmount,
                e.Deductions.Select(d => new DeductionDto(d.ChoreId, d.ChoreName, d.MissedPeriod, d.Amount)).ToList(),
                e.FinalAmount,
                e.TimestampUtc))
            .ToList();
        
        return new SalaryReportsResponse(reports);
    }
}

internal static class SalaryReportsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/reports", async (Guid householdId, Guid? memberId, Handler handler) =>
        {
            var query = new GetSalaryReportsQuery(householdId, memberId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
