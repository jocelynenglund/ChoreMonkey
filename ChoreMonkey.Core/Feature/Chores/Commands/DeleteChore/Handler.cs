using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.DeleteChore;

public record DeleteChoreCommand(Guid HouseholdId, Guid ChoreId, int PinCode);
public record DeleteChoreResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(DeleteChoreCommand request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var householdCreated = householdEvents
            .OfType<HouseholdCreated>()
            .FirstOrDefault();

        if (householdCreated == null)
        {
            return (false, false);
        }

        // Check for admin PIN changes
        var adminPinChanged = householdEvents
            .OfType<AdminPinChanged>()
            .LastOrDefault();
        var currentAdminPinHash = adminPinChanged?.NewPinHash ?? householdCreated.PinHash;

        // Verify admin access
        var isAdmin = PinHasher.VerifyPin(request.PinCode, currentAdminPinHash);
        if (!isAdmin)
        {
            return (false, false);
        }

        // Check chore exists
        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        var choreExists = choreEvents
            .OfType<ChoreCreated>()
            .Any(c => c.ChoreId == request.ChoreId);

        if (!choreExists)
        {
            return (true, false);
        }

        // Already deleted?
        var alreadyDeleted = choreEvents
            .OfType<ChoreDeleted>()
            .Any(c => c.ChoreId == request.ChoreId);

        if (alreadyDeleted)
        {
            return (true, true); // Already deleted, consider success
        }

        // Delete the chore
        var deleteEvent = new ChoreDeleted(request.ChoreId, request.HouseholdId, Guid.Empty);
        await store.AppendToStreamAsync(choreStreamId, deleteEvent, ExpectedVersion.Any);

        return (true, true);
    }
}

internal static class DeleteChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/delete", 
            async (Guid householdId, Guid choreId, DeleteChoreRequest dto, Handler handler) =>
        {
            var command = new DeleteChoreCommand(householdId, choreId, dto.PinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.StatusCode(403); // Forbidden - not admin
            }

            if (!success)
            {
                return Results.NotFound();
            }

            return Results.Ok(new DeleteChoreResponse(true));
        });
    }
}

public record DeleteChoreRequest(int PinCode);
