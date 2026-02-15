using FileEventStore;
using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
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
    string Type,           // "completion" | "member_joined" | "chore_assigned" | "nickname_changed" | "status_changed" | "chore_created"
    DateTimeOffset Timestamp,
    Guid? ChoreId,
    string? ChoreName,
    Guid? MemberId,
    string? MemberNickname,
    string? Text = null,   // Pre-rendered display text from ActivityRecorded
    string[]? AssignedToNicknames = null,  // For legacy format
    bool? AssignedToAll = null,
    string? OldNickname = null,
    string? NewNickname = null,
    string? AssignedByNickname = null,
    bool? IsClaimed = null,
    string? Status = null
);

public record GetCompletionTimelineResponse(
    List<CompletionEntry> Completions,
    List<ActivityEntry>? Activities = null
);

internal class Handler(IEventStore store)
{
    private static string ActivityStreamId(Guid householdId) => $"activities-{householdId}";

    public async Task<GetCompletionTimelineResponse> HandleAsync(GetCompletionTimelineQuery request)
    {
        var maxDays = request.Days ?? 7;
        var maxItems = request.Limit ?? 50;
        var cutoff = DateTime.UtcNow.AddDays(-maxDays);

        // Try to read from the new activity stream first
        var activityStreamId = ActivityStreamId(request.HouseholdId);
        var activityEvents = await store.FetchEventsAsync(activityStreamId);
        
        if (activityEvents.Any())
        {
            // Use the new immutable activity stream
            var activities = activityEvents
                .OfType<ActivityRecorded>()
                .Where(a => DateTime.TryParse(a.TimestampUtc, out var ts) && ts >= cutoff)
                .OrderByDescending(a => DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.UtcNow)
                .Take(maxItems)
                .Select(a => new ActivityEntry(
                    a.Type,
                    DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                    a.ChoreId,
                    a.ChoreName,
                    a.ActorId,
                    a.ActorNickname,
                    a.Text  // The pre-rendered text
                ))
                .ToList();

            // Also get completions for backwards compat
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

        // Fallback: Build from source events (legacy behavior)
        return await BuildFromSourceEvents(request, cutoff, maxItems);
    }

    private async Task<GetCompletionTimelineResponse> BuildFromSourceEvents(
        GetCompletionTimelineQuery request, DateTime cutoff, int maxItems)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Get all chore names
        var choreNames = choreEvents
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        // Get all member nicknames (current state - this is the legacy bug)
        var memberNicknames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(m => m.MemberId, m => m.Nickname);
        
        // Apply nickname changes (gets current state, not historical)
        foreach (var change in householdEvents.OfType<MemberNicknameChanged>())
        {
            memberNicknames[change.MemberId] = change.NewNickname;
        }

        // Get all completions within time range, sorted by most recent
        var completions = choreEvents
            .OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt >= cutoff)
            .OrderByDescending(c => c.CompletedAt)
            .Take(maxItems)
            .Select(c => new CompletionEntry(
                c.ChoreId,
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown"),
                c.CompletedByMemberId,
                memberNicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown"),
                c.CompletedAt
            ))
            .ToList();

        // Build unified activity feed
        var completionActivities = choreEvents
            .OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt >= cutoff)
            .Select(c => new ActivityEntry(
                "completion",
                c.CompletedAt,
                c.ChoreId,
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown"),
                c.CompletedByMemberId,
                memberNicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown"),
                $"{memberNicknames.GetValueOrDefault(c.CompletedByMemberId, "Someone")} completed {choreNames.GetValueOrDefault(c.ChoreId, "a chore")}"
            ));

        var joinActivities = householdEvents
            .OfType<MemberJoinedHousehold>()
            .Where(m => DateTime.TryParse(m.TimestampUtc, out var ts) && ts >= cutoff)
            .Select(m => new ActivityEntry(
                "member_joined",
                DateTime.TryParse(m.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                null,
                null,
                m.MemberId,
                m.Nickname,
                $"{m.Nickname} joined the household"
            ));

        var assignmentActivities = choreEvents
            .OfType<ChoreAssigned>()
            .Where(a => DateTime.TryParse(a.TimestampUtc, out var ts) && ts >= cutoff)
            .Select(a => {
                var assignedNicknames = a.AssignToAll 
                    ? new[] { "everyone" }
                    : a.AssignedToMemberIds?
                        .Select(id => memberNicknames.GetValueOrDefault(id, "Unknown"))
                        .ToArray() ?? Array.Empty<string>();
                
                var assignerNickname = a.AssignedByMemberId.HasValue 
                    ? memberNicknames.GetValueOrDefault(a.AssignedByMemberId.Value, null)
                    : null;
                
                var isClaimed = !a.AssignToAll 
                    && a.AssignedToMemberIds?.Length == 1 
                    && a.AssignedByMemberId.HasValue
                    && a.AssignedToMemberIds[0] == a.AssignedByMemberId.Value;

                var text = isClaimed
                    ? $"{assignerNickname} claimed {choreNames.GetValueOrDefault(a.ChoreId, "a chore")}"
                    : a.AssignToAll
                        ? $"{choreNames.GetValueOrDefault(a.ChoreId, "a chore")} assigned to everyone"
                        : $"{choreNames.GetValueOrDefault(a.ChoreId, "a chore")} assigned to {string.Join(", ", assignedNicknames)}";
                
                return new ActivityEntry(
                    "chore_assigned",
                    DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                    a.ChoreId,
                    choreNames.GetValueOrDefault(a.ChoreId, "Unknown"),
                    a.AssignedByMemberId,
                    assignerNickname,
                    text,
                    assignedNicknames,
                    a.AssignToAll,
                    null,
                    null,
                    assignerNickname,
                    isClaimed
                );
            });

        var nicknameActivities = householdEvents
            .OfType<MemberNicknameChanged>()
            .Where(n => DateTime.TryParse(n.TimestampUtc, out var ts) && ts >= cutoff)
            .Select(n => new ActivityEntry(
                "nickname_changed",
                DateTime.TryParse(n.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                null,
                null,
                n.MemberId,
                n.NewNickname,
                $"{n.OldNickname} is now {n.NewNickname}",
                null,
                null,
                n.OldNickname,
                n.NewNickname
            ));

        var statusActivities = householdEvents
            .OfType<MemberStatusChanged>()
            .Where(s => DateTime.TryParse(s.TimestampUtc, out var ts) && ts >= cutoff)
            .Select(s => new ActivityEntry(
                "status_changed",
                DateTime.TryParse(s.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                null,
                null,
                s.MemberId,
                memberNicknames.GetValueOrDefault(s.MemberId, "Unknown"),
                $"{memberNicknames.GetValueOrDefault(s.MemberId, "Someone")}: {s.Status}",
                null,
                null,
                null,
                null,
                null,
                null,
                s.Status
            ));

        var activities = completionActivities
            .Concat(joinActivities)
            .Concat(assignmentActivities)
            .Concat(nicknameActivities)
            .Concat(statusActivities)
            .OrderByDescending(a => a.Timestamp)
            .Take(maxItems)
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
