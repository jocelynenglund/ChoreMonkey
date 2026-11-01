using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.MyChores;

public record GetMyChoresQuery(Guid HouseholdId, Guid MemberId);

public record MyChoreDto(
    Guid ChoreId,
    string DisplayName,
    string? FrequencyType,
    string? DueDescription);

public record MyOverdueChoreDto(
    Guid ChoreId,
    string DisplayName,
    string? FrequencyType,
    string OverduePeriod,
    string PeriodKey);  // For acknowledge-missed: "2024-02-13" or "2024-W06"

public record MyCompletedChoreDto(
    Guid ChoreId,
    string DisplayName,
    DateTime CompletedAt,
    string? CompletedByName = null);  // null = completed by me; set when someone else did a shared chore

public record GetMyChoresResponse(
    List<MyChoreDto> Pending,
    List<MyOverdueChoreDto> Overdue,
    List<MyCompletedChoreDto> Completed);

internal class Handler(IEventStore store)
{
    public async Task<GetMyChoresResponse?> HandleAsync(GetMyChoresQuery request)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return MyChoresView.Project(history, request.MemberId, DateTime.UtcNow);
    }
}

internal static class MyChoresEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/my-chores", async (
            Guid householdId,
            Guid memberId,
            Handler handler) =>
        {
            var query = new GetMyChoresQuery(householdId, memberId);
            var result = await handler.HandleAsync(query);
            
            if (result == null)
                return Results.NotFound();
            
            return Results.Ok(result);
        });
    }
}
