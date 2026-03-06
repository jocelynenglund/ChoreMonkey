using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.SalaryReports.Queries.SalarySettings;

public record GetSalarySettingsQuery(Guid HouseholdId);

public record SalarySettingsResponse(
    bool Enabled,
    decimal BaseAmount);

internal class Handler(IEventStore store)
{
    public async Task<SalarySettingsResponse> HandleAsync(GetSalarySettingsQuery request)
    {
        var streamId = SalaryReportsAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        
        var enabledEvent = events.OfType<SalaryReportsEnabled>().FirstOrDefault();
        if (enabledEvent == null)
        {
            return new SalarySettingsResponse(false, 800m);
        }
        
        // Get current base amount (could have been updated)
        var baseAmount = events.OfType<BaseSalaryUpdated>()
            .Select(e => (decimal?)e.NewBaseAmount)
            .LastOrDefault() ?? enabledEvent.BaseAmount;
        
        return new SalarySettingsResponse(true, baseAmount);
    }
}

internal static class SalarySettingsEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/settings", async (Guid householdId, Handler handler) =>
        {
            var query = new GetSalarySettingsQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
