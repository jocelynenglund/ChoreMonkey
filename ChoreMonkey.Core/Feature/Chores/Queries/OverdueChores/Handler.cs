using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.OverdueChores;

public record GetOverdueQuery(Guid HouseholdId, int PinCode);

public record OverdueChoreDto(
    Guid ChoreId,
    string DisplayName,
    string OverduePeriod,
    DateTime? LastCompleted);

public record MemberOverdueDto(
    Guid MemberId,
    string Nickname,
    int OverdueCount,
    List<OverdueChoreDto> Chores);

public record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);

internal class Handler(IEventStore store)
{
    public async Task<GetOverdueResponse?> HandleAsync(GetOverdueQuery request)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return OverdueChoresView.Project(history, request.PinCode, DateTime.UtcNow);
    }
}

internal static class OverdueChoresEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/overdue", async (
            Guid householdId,
            HttpContext context,
            Handler handler) =>
        {
            var pinCodeHeader = context.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCodeHeader) || !int.TryParse(pinCodeHeader, out var pinCode))
            {
                return Results.Json(new { error = "PIN required" }, statusCode: 401);
            }
            
            var query = new GetOverdueQuery(householdId, pinCode);
            var result = await handler.HandleAsync(query);
            
            if (result == null)
            {
                return Results.Json(new { error = "Admin access required" }, statusCode: 403);
            }
            
            return Results.Ok(result);
        });
    }
}
