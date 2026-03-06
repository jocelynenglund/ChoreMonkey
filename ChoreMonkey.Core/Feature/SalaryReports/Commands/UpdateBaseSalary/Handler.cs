using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.SalaryReports.Commands.UpdateBaseSalary;

public record UpdateBaseSalaryCommand(Guid HouseholdId, decimal NewBaseAmount);
public record UpdateBaseSalaryRequest(decimal NewBaseAmount);
public record UpdateBaseSalaryResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<UpdateBaseSalaryResponse> HandleAsync(UpdateBaseSalaryCommand request)
    {
        var streamId = SalaryReportsAggregate.StreamId(request.HouseholdId);
        
        // Verify salary reports are enabled
        var events = await store.FetchEventsAsync(streamId);
        if (!events.OfType<SalaryReportsEnabled>().Any())
        {
            return new UpdateBaseSalaryResponse(false);
        }
        
        var salaryUpdated = new BaseSalaryUpdated(request.HouseholdId, request.NewBaseAmount);
        await store.AppendToStreamAsync(streamId, salaryUpdated, ExpectedVersion.Any);
        
        return new UpdateBaseSalaryResponse(true);
    }
}

internal static class UpdateBaseSalaryEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/base", async (Guid householdId, UpdateBaseSalaryRequest dto, Handler handler) =>
        {
            var command = new UpdateBaseSalaryCommand(householdId, dto.NewBaseAmount);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
