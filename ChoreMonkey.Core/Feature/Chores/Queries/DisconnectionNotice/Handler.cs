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
    public const int WindowMonths = 2;

    public async Task<GetDisconnectionNoticeResponse?> HandleAsync(GetDisconnectionNoticeQuery request)
    {
        var householdEvents = await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId));
        var choreEvents = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var memberExists = householdEvents.OfType<MemberJoinedHousehold>()
            .Any(e => e.MemberId == request.MemberId);
        if (!memberExists) return null;

        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddMonths(-WindowMonths);

        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        foreach (var update in choreEvents.OfType<ChoreUpdated>())
            if (chores.ContainsKey(update.ChoreId))
                chores[update.ChoreId] = chores[update.ChoreId] with
                {
                    DisplayName = update.DisplayName, Description = update.Description,
                    Frequency = update.Frequency, IsOptional = update.IsOptional,
                    StartDate = update.StartDate, IsRequired = update.IsRequired,
                    MissedDeduction = update.MissedDeduction,
                };

        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        var chorePauses = ChorePauses.Build(choreEvents);

        var completionsByChore = choreEvents.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var missed = new List<DisconnectedChoreDto>();
        var anyCompletedInWindow = false;
        var anyOlderThanWindow = false;

        foreach (var (choreId, chore) in chores)
        {
            if (chore.IsOptional) continue;

            var type = chore.Frequency?.Type?.ToLower();
            if (type is null or "once") continue;

            var assignment = assignments.GetValueOrDefault(choreId);
            var isShared = assignment?.AssignToAll == true;
            var isAssignedToMe = isShared ||
                (assignment?.AssignedToMemberIds?.Contains(request.MemberId) ?? false);
            if (!isAssignedToMe) continue;

            // Excused: any pause overlapping the window takes the chore out of the reckoning.
            if (chorePauses.GetValueOrDefault(choreId).CoversAnyInRange(windowStart, today))
                continue;

            // For shared chores, anyone in the household doing it counts.
            var completions = (completionsByChore.GetValueOrDefault(choreId) ?? [])
                .Where(c => isShared || c.CompletedByMemberId == request.MemberId)
                .ToList();

            if (completions.Any(c => c.CompletedAt.Date >= windowStart))
            {
                anyCompletedInWindow = true;
                continue;
            }

            var choreStartDate = chore.StartDate?.Date
                ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : today);
            if (choreStartDate <= windowStart)
                anyOlderThanWindow = true;

            missed.Add(new DisconnectedChoreDto(
                choreId,
                chore.DisplayName,
                chore.Frequency?.Type,
                completions.MaxBy(c => c.CompletedAt)?.CompletedAt));
        }

        // Only cut off when there's been a full two-month stretch with nothing done.
        var isDisconnected = !anyCompletedInWindow && anyOlderThanWindow;

        return new GetDisconnectionNoticeResponse(
            isDisconnected,
            windowStart,
            isDisconnected ? missed.OrderBy(c => c.DisplayName).ToList() : []);
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
