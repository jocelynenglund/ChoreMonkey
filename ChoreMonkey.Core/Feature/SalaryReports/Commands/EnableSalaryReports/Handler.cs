using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.SalaryReports.Commands.EnableSalaryReports;

public record EnableSalaryReportsCommand(Guid HouseholdId, decimal BaseAmount = 800m);
public record EnableSalaryReportsRequest(decimal BaseAmount = 800m);
public record EnableSalaryReportsResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<EnableSalaryReportsResponse> HandleAsync(EnableSalaryReportsCommand request)
    {
        var streamId = SalaryReportsAggregate.StreamId(request.HouseholdId);
        
        // Check if already enabled
        var events = await store.FetchEventsAsync(streamId);
        if (events.OfType<SalaryReportsEnabled>().Any())
        {
            return new EnableSalaryReportsResponse(false);
        }
        
        var salaryEnabled = new SalaryReportsEnabled(request.HouseholdId, request.BaseAmount);
        await store.AppendToStreamAsync(streamId, salaryEnabled, ExpectedVersion.Any);
        
        return new EnableSalaryReportsResponse(true);
    }
}

internal static class EnableSalaryReportsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/enable", async (Guid householdId, EnableSalaryReportsRequest dto, Handler handler) =>
        {
            var command = new EnableSalaryReportsCommand(householdId, dto.BaseAmount);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
