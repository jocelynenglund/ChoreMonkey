using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.PauseChore;

public record PauseChoreCommand(
    Guid HouseholdId,
    Guid ChoreId,
    Guid PauseId,
    DateTime PauseStart,
    DateTime PauseEnd,
    string? Reason,
    int PinCode);

public record PauseChoreRequest(
    DateTime PauseStart,
    DateTime PauseEnd,
    int PinCode,
    string? Reason = null,
    Guid? PauseId = null);

public record PauseChoreResponse(Guid PauseId);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, bool Success, string? Error, Guid PauseId)> HandleAsync(PauseChoreCommand request)
    {
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();
        if (householdCreated == null)
        {
            return (false, false, "Household not found", Guid.Empty);
        }

        var adminPinHash = householdEvents.OfType<AdminPinChanged>()
            .LastOrDefault()?.NewPinHash ?? householdCreated.PinHash;
        if (!PinHasher.VerifyPin(request.PinCode, adminPinHash))
        {
            return (false, false, "Admin access required", Guid.Empty);
        }

        // Normalize to whole days; pause windows are inclusive of both ends.
        var start = request.PauseStart.Date;
        var end = request.PauseEnd.Date;
        if (end < start)
        {
            return (true, false, "Pause end date must be on or after the start date", Guid.Empty);
        }

        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();
        var choreExists = choreEvents.OfType<ChoreCreated>()
            .Any(c => c.ChoreId == request.ChoreId && !deletedChoreIds.Contains(c.ChoreId));
        if (!choreExists)
        {
            return (true, false, "Chore not found", Guid.Empty);
        }

        var pauseId = request.PauseId == Guid.Empty ? Guid.NewGuid() : request.PauseId;

        var paused = new ChorePaused(
            pauseId,
            request.ChoreId,
            request.HouseholdId,
            start,
            end,
            string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

        await store.AppendToStreamAsync(choreStreamId, paused, ExpectedVersion.Any);

        return (true, true, null, pauseId);
    }
}

internal static class PauseChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores/{choreId:guid}/pause",
            async (Guid householdId, Guid choreId, PauseChoreRequest dto, Handler handler) =>
        {
            var command = new PauseChoreCommand(
                householdId,
                choreId,
                dto.PauseId ?? Guid.NewGuid(),
                dto.PauseStart,
                dto.PauseEnd,
                dto.Reason,
                dto.PinCode);

            var (isAdmin, success, error, pauseId) = await handler.HandleAsync(command);

            if (!isAdmin)
            {
                return Results.StatusCode(403);
            }

            if (!success)
            {
                return Results.BadRequest(new { error });
            }

            return Results.Ok(new PauseChoreResponse(pauseId));
        });
    }
}
