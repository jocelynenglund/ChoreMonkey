using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.AssignChore;

public record AssignChoreCommand(
    Guid HouseholdId, 
    Guid ChoreId, 
    Guid[]? MemberIds = null,
    bool AssignToAll = false,
    Guid? AssignedByMemberId = null);
    
public record AssignChoreResponse(
    Guid ChoreId, 
    Guid[]? AssignedTo,
    bool AssignedToAll);

internal class Handler(IEventStore store)
{
    public async Task<AssignChoreResponse> HandleAsync(AssignChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        var assignedEvent = new ChoreAssigned(
            request.ChoreId, 
            request.HouseholdId, 
            request.MemberIds,
            request.AssignToAll,
            request.AssignedByMemberId);
            
        await store.AppendToStreamAsync(streamId, assignedEvent, ExpectedVersion.Any);
        
        return new AssignChoreResponse(request.ChoreId, request.MemberIds, request.AssignToAll);
    }
}

internal static class AssignChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/assign", async (
            Guid householdId,
            Guid choreId,
            AssignChoreRequest request,
            Handler handler) =>
        {
            var command = new AssignChoreCommand(
                householdId, 
                choreId, 
                request.MemberIds,
                request.AssignToAll,
                request.AssignedByMemberId);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}

public record AssignChoreRequest(
    Guid[]? MemberIds = null,
    bool AssignToAll = false,
    Guid? AssignedByMemberId = null);
