using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.CompleteChore;

public record CompleteChoreCommand(
    Guid HouseholdId, 
    Guid ChoreId, 
    Guid MemberId,
    DateTime? CompletedAt = null);

public record CompleteChoreRequest(
    Guid MemberId,
    DateTime? CompletedAt = null);

public record CompleteChoreResponse(
    Guid ChoreId, 
    Guid CompletedBy, 
    DateTime CompletedAt);

internal class Handler(IEventStore store)
{
    public async Task<CompleteChoreResponse> HandleAsync(CompleteChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var completedAt = request.CompletedAt ?? DateTime.UtcNow;
        
        // Check current assignments
        var events = await store.FetchEventsAsync(streamId);
        var currentAssignment = events
            .OfType<ChoreAssigned>()
            .Where(e => e.ChoreId == request.ChoreId)
            .LastOrDefault();
        
        var isAssigned = currentAssignment?.AssignToAll == true ||
            (currentAssignment?.AssignedToMemberIds?.Contains(request.MemberId) ?? false);
        
        // If not assigned, auto-assign this member first
        if (!isAssigned)
        {
            var newAssignees = currentAssignment?.AssignedToMemberIds?.ToList() ?? new List<Guid>();
            newAssignees.Add(request.MemberId);
            
            var assignEvent = new ChoreAssigned(
                request.ChoreId,
                request.HouseholdId,
                newAssignees.ToArray(),
                currentAssignment?.AssignToAll ?? false);
                
            await store.AppendToStreamAsync(streamId, assignEvent, ExpectedVersion.Any);
        }
        
        // Now complete the chore
        var completedEvent = new ChoreCompleted(
            request.ChoreId,
            request.HouseholdId,
            request.MemberId,
            completedAt);
            
        await store.AppendToStreamAsync(streamId, completedEvent, ExpectedVersion.Any);
        
        return new CompleteChoreResponse(request.ChoreId, request.MemberId, completedAt);
    }
}

internal static class CompleteChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/complete", async (
            Guid householdId,
            Guid choreId,
            CompleteChoreRequest request,
            Handler handler) =>
        {
            var command = new CompleteChoreCommand(
                householdId, 
                choreId, 
                request.MemberId,
                request.CompletedAt);
                
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
