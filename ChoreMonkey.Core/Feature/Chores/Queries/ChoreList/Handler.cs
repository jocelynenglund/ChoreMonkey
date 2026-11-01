using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace ChoreMonkey.Core.Feature.Chores.Queries.ChoreList;

public record GetChoresQuery(Guid HouseholdId);

public record ChoreDto(
    Guid ChoreId, 
    string DisplayName, 
    string Description, 
    Guid[]? AssignedTo = null,
    bool AssignedToAll = false,
    FrequencyDto? Frequency = null,
    DateTime? LastCompletedAt = null,
    Guid? LastCompletedBy = null,
    List<MemberCompletionDto>? MemberCompletions = null,
    bool IsOptional = false,
    bool IsRequired = true,
    decimal MissedDeduction = 10m,
    decimal? DeductionRate = null,
    decimal? BonusRate = null,
    List<PauseWindowDto>? Pauses = null);

public record PauseWindowDto(
    Guid PauseId,
    DateTime Start,
    DateTime End,
    string? Reason);

public record MemberCompletionDto(
    Guid MemberId,
    bool CompletedToday,
    bool CompletedThisWeek,
    DateTime? LastCompletedAt);

public record FrequencyDto(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

public record GetChoresResponse(List<ChoreDto> Chores);

internal class Handler(IEventStore store)
{
    public async Task<GetChoresResponse> HandleAsync(GetChoresQuery request)
    {
        var history = (await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .Concat((await store.FetchEventsAsync(SalaryAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return ChoreListView.Project(history, DateTime.UtcNow);
    }
}

internal static class ChoreListEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/chores", async (Guid householdId, Feature.Chores.Queries.ChoreList.Handler handler) =>
        {
            var query = new GetChoresQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
