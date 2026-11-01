using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Activity.Queries.TeamOverview;

public record TeamOverviewQuery(Guid HouseholdId, int PinCode);

public record ChoreStatusDto(
    Guid ChoreId,
    string DisplayName,
    string Status, // "completed", "pending", "overdue"
    string? OverduePeriod,
    DateTime? LastCompletedAt,
    bool IsOptional);

public record MemberOverviewDto(
    Guid MemberId,
    string Nickname,
    int TotalChores,
    int CompletedCount,
    int OverdueCount,
    List<ChoreStatusDto> Chores);

public record TeamOverviewResponse(List<MemberOverviewDto> Members);

internal class Handler(IEventStore store)
{
    public async Task<TeamOverviewResponse?> HandleAsync(TeamOverviewQuery request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>()
            .Concat((await store.FetchEventsAsync(choreStreamId)).Cast<object>())
            .ToList();
        return TeamOverviewView.Project(history, request.PinCode, DateTime.UtcNow);
    }
}

internal static class TeamOverviewEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/team", async (
            Guid householdId,
            HttpContext context,
            Handler handler) =>
        {
            var pinCodeHeader = context.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCodeHeader) || !int.TryParse(pinCodeHeader, out var pinCode))
            {
                return Results.Json(new { error = "PIN required" }, statusCode: 401);
            }

            var query = new TeamOverviewQuery(householdId, pinCode);
            var result = await handler.HandleAsync(query);

            if (result == null)
            {
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);
            }

            return Results.Ok(result);
        });
    }
}
