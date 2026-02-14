using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.CompleteChore;

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
