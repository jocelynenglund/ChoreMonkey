using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace ChoreMonkey.Core.Feature.ChoreList;

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
    List<MemberCompletionDto>? MemberCompletions = null);

public record MemberCompletionDto(
    Guid MemberId,
    bool CompletedToday,
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
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var events = await store.FetchEventsAsync(streamId);
        var today = DateTime.UtcNow.Date;

        // Get all created chores
        var createdChores = events.OfType<ChoreCreated>().ToList();
        
        // Get latest assignment for each chore
        var assignments = events.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Get all completions grouped by chore
        var completionsByChore = events.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chores = createdChores
            .Select(e => {
                var assignment = assignments.GetValueOrDefault(e.ChoreId);
                var choreCompletions = completionsByChore.GetValueOrDefault(e.ChoreId) ?? new List<ChoreCompleted>();
                var lastCompletion = choreCompletions.OrderByDescending(c => c.CompletedAt).FirstOrDefault();
                
                var frequency = e.Frequency != null 
                    ? new FrequencyDto(e.Frequency.Type, e.Frequency.Days, e.Frequency.IntervalDays)
                    : new FrequencyDto("once");
                
                // Build per-member completion status
                List<MemberCompletionDto>? memberCompletions = null;
                if (assignment?.AssignedToMemberIds != null || assignment?.AssignToAll == true)
                {
                    var assignedMembers = assignment.AssignedToMemberIds ?? Array.Empty<Guid>();
                    memberCompletions = assignedMembers.Select(memberId => {
                        var memberLastCompletion = choreCompletions
                            .Where(c => c.CompletedByMemberId == memberId)
                            .OrderByDescending(c => c.CompletedAt)
                            .FirstOrDefault();
                        var completedToday = memberLastCompletion?.CompletedAt.Date == today;
                        return new MemberCompletionDto(
                            memberId,
                            completedToday,
                            memberLastCompletion?.CompletedAt);
                    }).ToList();
                }
                    
                return new ChoreDto(
                    e.ChoreId, 
                    e.DisplayName, 
                    e.Description,
                    assignment?.AssignedToMemberIds,
                    assignment?.AssignToAll ?? false,
                    frequency,
                    lastCompletion?.CompletedAt,
                    lastCompletion?.CompletedByMemberId,
                    memberCompletions);
            })
            .ToList();

        return new GetChoresResponse(chores);
    }
}

internal static class ChoreListEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/chores", async (Guid householdId, Feature.ChoreList.Handler handler) =>
        {
            var query = new GetChoresQuery(householdId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
