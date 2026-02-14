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
    List<MemberCompletionDto>? MemberCompletions = null,
    bool IsOptional = false);

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
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    public async Task<GetChoresResponse> HandleAsync(GetChoresQuery request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        
        var events = await store.FetchEventsAsync(streamId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        
        var today = DateTime.UtcNow.Date;
        var weekStart = GetMondayOfWeek(today);
        var weekEnd = weekStart.AddDays(7);
        
        // Get all household members
        var allMemberIds = householdEvents.OfType<MemberJoinedHousehold>()
            .Select(e => e.MemberId)
            .ToArray();

        // Get all created chores, excluding deleted ones
        var deletedChoreIds = events.OfType<ChoreDeleted>()
            .Select(d => d.ChoreId)
            .ToHashSet();
        var createdChores = events.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToList();
        
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
                    // Use all members if AssignToAll, otherwise use assigned members
                    var assignedMembers = assignment.AssignToAll == true 
                        ? allMemberIds 
                        : (assignment.AssignedToMemberIds ?? Array.Empty<Guid>());
                    memberCompletions = assignedMembers.Select(memberId => {
                        var memberCompletionsList = choreCompletions
                            .Where(c => c.CompletedByMemberId == memberId)
                            .ToList();
                        var memberLastCompletion = memberCompletionsList
                            .OrderByDescending(c => c.CompletedAt)
                            .FirstOrDefault();
                        var completedToday = memberLastCompletion?.CompletedAt.Date == today;
                        var completedThisWeek = memberCompletionsList
                            .Any(c => c.CompletedAt.Date >= weekStart && c.CompletedAt.Date < weekEnd);
                        return new MemberCompletionDto(
                            memberId,
                            completedToday,
                            completedThisWeek,
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
                    memberCompletions,
                    e.IsOptional);
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
