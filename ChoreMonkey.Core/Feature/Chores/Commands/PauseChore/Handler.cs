using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
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

        var history = (await store.FetchEventsAsync(householdStreamId)).Cast<object>()
            .Concat((await store.FetchEventsAsync(choreStreamId)).Cast<object>())
            .ToList();

        var decision = PauseChoreSlice.Decide(history, request);
        if (decision.Kind == ErrorKind.Forbidden) return (false, false, decision.Error, Guid.Empty);
        if (!decision.Accepted) return (true, false, decision.Error, Guid.Empty);

        var paused = decision.Events.OfType<ChorePaused>().Single();
        await store.AppendToStreamAsync(choreStreamId, paused, ExpectedVersion.Any);

        return (true, true, null, paused.PauseId);
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
