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
    string Type,           // "completion" | "member_joined" | "chore_assigned" | "nickname_changed"
    DateTimeOffset Timestamp,
    Guid? ChoreId,
    string? ChoreName,
    Guid? MemberId,
    string? MemberNickname,
    string[]? AssignedToNicknames = null,  // For assignments
    bool? AssignedToAll = null,
    string? OldNickname = null,            // For nickname changes
    string? NewNickname = null,
    string? AssignedByNickname = null,     // Who assigned
    bool? IsClaimed = null                  // Self-assigned
);

public record GetCompletionTimelineResponse(
    List<CompletionEntry> Completions,
    List<ActivityEntry>? Activities = null
);

internal class Handler(IEventStore store)
{
    public async Task<GetCompletionTimelineResponse> HandleAsync(GetCompletionTimelineQuery request)
    {
        var maxDays = request.Days ?? 7;
        var maxItems = request.Limit ?? 50;
        var cutoff = DateTime.UtcNow.AddDays(-maxDays);

        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Get all chore names
        var choreNames = choreEvents
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        // Get all member nicknames
        var memberNicknames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(m => m.MemberId, m => m.Nickname);

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

        // Build unified activity feed (completions + joins)
        var completionActivities = choreEvents
            .OfType<ChoreCompleted>()
            .Where(c => c.CompletedAt >= cutoff)
            .Select(c => new ActivityEntry(
                "completion",
                c.CompletedAt,
                c.ChoreId,
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown"),
                c.CompletedByMemberId,
                memberNicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown")
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
                m.Nickname
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
                
                // Check if this is a self-claim (single assignee who is also the assigner)
                var isClaimed = !a.AssignToAll 
                    && a.AssignedToMemberIds?.Length == 1 
                    && a.AssignedByMemberId.HasValue
                    && a.AssignedToMemberIds[0] == a.AssignedByMemberId.Value;
                
                return new ActivityEntry(
                    "chore_assigned",
                    DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.UtcNow,
                    a.ChoreId,
                    choreNames.GetValueOrDefault(a.ChoreId, "Unknown"),
                    a.AssignedByMemberId,
                    assignerNickname,
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
                null,
                null,
                n.OldNickname,
                n.NewNickname
            ));

        var activities = completionActivities
            .Concat(joinActivities)
            .Concat(assignmentActivities)
            .Concat(nicknameActivities)
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
