using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.RemoveChorePause;

public record RemoveChorePauseCommand(Guid HouseholdId, Guid ChoreId, Guid PauseId, int PinCode);
public record RemoveChorePauseRequest(int PinCode);
public record RemoveChorePauseResponse(bool Success);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success)> HandleAsync(RemoveChorePauseCommand request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
        {
            return (false, false);
        }

        var adminPinHash = householdEvents.OfType<AdminPinChanged>()
            .LastOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        if (!PinHasher.VerifyPin(request.PinCode, adminPinHash))
        {
            return (false, false);
        }

        var choreEvents = await store.FetchEventsAsync(choreStreamId);

        // Idempotent: only an active (created, not-yet-removed) pause can be removed.
        var pauses = ChorePauses.Build(choreEvents);
        var isActive = pauses.TryGetValue(request.ChoreId, out var windows)
            && windows.Any(w => w.PauseId == request.PauseId);
        if (!isActive)
        {
            return (true, false);
        }

        var removed = new ChorePauseRemoved(request.PauseId, request.ChoreId, request.HouseholdId);
        await store.AppendToStreamAsync(choreStreamId, removed, ExpectedVersion.Any);

        return (true, true);
    }
}

internal static class RemoveChorePauseEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/pause/{pauseId:guid}/remove",
            async (Guid householdId, Guid choreId, Guid pauseId, RemoveChorePauseRequest dto, Handler handler) =>
        {
            var command = new RemoveChorePauseCommand(householdId, choreId, pauseId, dto.PinCode);
            var (isAdmin, success) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.StatusCode(403);
            }

            if (!success)
            {
                return Results.NotFound();
            }

            return Results.Ok(new RemoveChorePauseResponse(true));
        });
    }
}
