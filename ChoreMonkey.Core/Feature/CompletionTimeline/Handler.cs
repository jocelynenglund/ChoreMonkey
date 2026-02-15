using ChoreMonkey.Core.Infrastructure.ReadModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.CompletionTimeline;

public record GetCompletionTimelineQuery(Guid HouseholdId, int? Limit = 50, int? Days = 7);

public record CompletionEntry(
    Guid ChoreId,
    string ChoreName,
    Guid CompletedBy,
    string CompletedByNickname,
    DateTimeOffset CompletedAt
);

public record ActivityEntry(
    string Type,
    DateTimeOffset Timestamp,
    Guid? ChoreId,
    string? ChoreName,
    Guid? MemberId,
    string? MemberNickname,
    string? Text = null
);

public record GetCompletionTimelineResponse(
    List<CompletionEntry> Completions,
    List<ActivityEntry>? Activities = null
);

internal class Handler(IActivityReadModel activityReadModel)
{
    public async Task<GetCompletionTimelineResponse> HandleAsync(GetCompletionTimelineQuery request)
    {
        var items = await activityReadModel.GetActivitiesAsync(
            request.HouseholdId, 
            request.Days, 
            request.Limit);

        var activities = items.Select(a => new ActivityEntry(
            a.Type,
            DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
            a.ChoreId,
            a.ChoreName,
            a.ActorId,
            a.ActorNickname,
            a.Text
        )).ToList();

        // Extract completions for backwards compat
        var completions = activities
            .Where(a => a.Type == "completion" && a.ChoreId.HasValue && a.MemberId.HasValue)
            .Select(a => new CompletionEntry(
                a.ChoreId!.Value,
                a.ChoreName ?? "Unknown",
                a.MemberId!.Value,
                a.MemberNickname ?? "Unknown",
                a.Timestamp
            ))
            .ToList();

        return new GetCompletionTimelineResponse(completions, activities);
    }
}

public static class CompletionTimelineEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{householdId}/completions", async (
            Guid householdId,
            Handler handler,
            int? limit,
            int? days) =>
        {
            var response = await handler.HandleAsync(
                new GetCompletionTimelineQuery(householdId, limit, days));
            return Results.Ok(response);
        });
    }
}
