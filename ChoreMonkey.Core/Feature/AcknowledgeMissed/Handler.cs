using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.AcknowledgeMissed;

public record AcknowledgeMissedCommand(Guid HouseholdId, Guid ChoreId, Guid MemberId, string Period);
public record AcknowledgeMissedResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<AcknowledgeMissedResponse> HandleAsync(AcknowledgeMissedCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        var acknowledged = new ChoreMissedAcknowledged(
            request.ChoreId,
            request.MemberId,
            request.Period);
        
        await store.AppendToStreamAsync(streamId, acknowledged, ExpectedVersion.Any);
        
        return new AcknowledgeMissedResponse(true);
    }
    
    /// <summary>
    /// Get the period string for a date based on frequency type.
    /// Daily: "2024-02-13"
    /// Weekly: "2024-W06"
    /// </summary>
    public static string GetPeriodString(DateTime date, string frequencyType)
    {
        return frequencyType.ToLower() switch
        {
            "daily" => date.ToString("yyyy-MM-dd"),
            "weekly" => GetWeekPeriod(date),
            _ => date.ToString("yyyy-MM-dd")
        };
    }
    
    private static string GetWeekPeriod(DateTime date)
    {
        // ISO week number
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }
}

internal static class AcknowledgeMissedEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/acknowledge-missed", async (
            Guid householdId,
            Guid choreId,
            AcknowledgeMissedRequest dto,
            Handler handler) =>
        {
            var command = new AcknowledgeMissedCommand(householdId, choreId, dto.MemberId, dto.Period);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}

public record AcknowledgeMissedRequest(Guid MemberId, string Period);
