using FileEventStore;
using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.CompletionTimeline;

public record GetCompletionTimelineQuery(Guid HouseholdId, int? Limit = 50, int? Days = 7);

public record ActivityEntry(
    string Type,           // "completion", "assignment", "nickname_change", "status_change", "member_joined", "chore_created"
    string Description,    // Human-readable description
    DateTimeOffset Timestamp,
    Guid? ChoreId = null,
    Guid? MemberId = null
);

public record GetCompletionTimelineResponse(List<ActivityEntry> Activities);

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

        // Build lookup dictionaries
        var choreNames = choreEvents
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        var memberNicknames = householdEvents
            .OfType<MemberJoinedHousehold>()
            .ToDictionary(m => m.MemberId, m => m.Nickname);

        // Apply nickname changes
        foreach (var change in householdEvents.OfType<MemberNicknameChanged>())
        {
            memberNicknames[change.MemberId] = change.NewNickname;
        }

        var activities = new List<ActivityEntry>();

        // Chore completions
        foreach (var e in choreEvents.OfType<ChoreCompleted>())
        {
            var ts = e.CompletedAt;
            if (ts < cutoff) continue;
            
            var choreName = choreNames.GetValueOrDefault(e.ChoreId, "Unknown chore");
            var memberName = memberNicknames.GetValueOrDefault(e.CompletedByMemberId, "Unknown");
            
            activities.Add(new ActivityEntry(
                "completion",
                $"{memberName} completed {choreName}",
                ts,
                e.ChoreId,
                e.CompletedByMemberId
            ));
        }

        // Chore assignments
        foreach (var e in choreEvents.OfType<ChoreAssigned>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;
            
            var choreName = choreNames.GetValueOrDefault(e.ChoreId, "Unknown chore");
            string description;
            
            if (e.AssignToAll)
            {
                description = $"{choreName} assigned to everyone";
            }
            else if (e.AssignedToMemberIds?.Length > 0)
            {
                var names = e.AssignedToMemberIds
                    .Select(id => memberNicknames.GetValueOrDefault(id, "Unknown"))
                    .ToList();
                description = $"{choreName} assigned to {string.Join(", ", names)}";
            }
            else
            {
                description = $"{choreName} unassigned";
            }
            
            activities.Add(new ActivityEntry(
                "assignment",
                description,
                ts,
                e.ChoreId
            ));
        }

        // Nickname changes
        foreach (var e in householdEvents.OfType<MemberNicknameChanged>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;
            
            activities.Add(new ActivityEntry(
                "nickname_change",
                $"{e.OldNickname} changed name to {e.NewNickname}",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Status changes
        foreach (var e in householdEvents.OfType<MemberStatusChanged>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;
            
            var memberName = memberNicknames.GetValueOrDefault(e.MemberId, "Unknown");
            
            activities.Add(new ActivityEntry(
                "status_change",
                $"{memberName} updated status: \"{e.Status}\"",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Member joined
        foreach (var e in householdEvents.OfType<MemberJoinedHousehold>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;
            
            activities.Add(new ActivityEntry(
                "member_joined",
                $"{e.Nickname} joined the household",
                ts,
                MemberId: e.MemberId
            ));
        }

        // Chore created
        foreach (var e in choreEvents.OfType<ChoreCreated>())
        {
            var ts = ParseTimestamp(e.TimestampUtc);
            if (ts < cutoff) continue;
            
            activities.Add(new ActivityEntry(
                "chore_created",
                $"New chore added: {e.DisplayName}",
                ts,
                e.ChoreId
            ));
        }

        // Sort by timestamp descending, take limit
        var result = activities
            .OrderByDescending(a => a.Timestamp)
            .Take(maxItems)
            .ToList();

        return new GetCompletionTimelineResponse(result);
    }

    private static DateTimeOffset ParseTimestamp(string timestampUtc)
    {
        if (DateTimeOffset.TryParse(timestampUtc, out var ts))
            return ts;
        return DateTimeOffset.MinValue;
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
