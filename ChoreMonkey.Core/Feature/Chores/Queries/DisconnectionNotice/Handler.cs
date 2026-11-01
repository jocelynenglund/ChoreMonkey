using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Queries.DisconnectionNotice;

/// <summary>
/// "Notice of disconnection": a member who hasn't done a single one of their
/// obligatory chores in the past two months gets their (pretend) internet cut
/// off until they complete one. Obligatory = assigned, recurring, not optional.
/// Chores paused at any point in the window are excused and don't count.
/// </summary>
public record GetDisconnectionNoticeQuery(Guid HouseholdId, Guid MemberId);

public record DisconnectedChoreDto(
    Guid ChoreId,
    string DisplayName,
    string? FrequencyType,
    DateTime? LastCompleted);

public record GetDisconnectionNoticeResponse(
    bool IsDisconnected,
    DateTime WindowStart,
    List<DisconnectedChoreDto> MissedChores);

internal class Handler(IEventStore store)
{
    public const int WindowMonths = DisconnectionNoticeView.WindowMonths;

    public async Task<GetDisconnectionNoticeResponse?> HandleAsync(GetDisconnectionNoticeQuery request)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return DisconnectionNoticeView.Project(history, request.MemberId, DateTime.UtcNow);
    }
}

internal static class DisconnectionNoticeEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/disconnection-notice", async (
            Guid householdId,
            Guid memberId,
            Handler handler) =>
        {
            var result = await handler.HandleAsync(new GetDisconnectionNoticeQuery(householdId, memberId));
            return result == null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
