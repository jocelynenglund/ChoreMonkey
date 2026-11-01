using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Chores.Commands.UpdateChore;

public record UpdateChoreCommand(
    Guid HouseholdId,
    Guid ChoreId,
    string DisplayName,
    string Description,
    ChoreFrequency? Frequency,
    bool IsOptional,
    DateTime? StartDate,
    bool IsRequired,
    decimal MissedDeduction);

public record UpdateChoreRequest(
    string DisplayName,
    string Description,
    FrequencyRequest? Frequency,
    bool IsOptional,
    DateTime? StartDate,
    bool IsRequired,
    decimal MissedDeduction);

public record FrequencyRequest(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

internal class Handler(IEventStore store)
{
    public async Task<IResult> HandleAsync(UpdateChoreCommand request)
    {
        var streamId = ChoreAggregate.StreamId(request.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        var decision = UpdateChoreSlice.Decide(history, request);
        if (!decision.Accepted)
            return Results.NotFound(new { error = decision.Error });

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);
        return Results.Ok();
    }
}

internal static class UpdateChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPut("households/{householdId:guid}/chores/{choreId:guid}", async (
            Guid householdId,
            Guid choreId,
            UpdateChoreRequest dto,
            Handler handler) =>
        {
            var frequency = dto.Frequency != null
                ? new ChoreFrequency(dto.Frequency.Type, dto.Frequency.Days, dto.Frequency.IntervalDays)
                : null;

            var command = new UpdateChoreCommand(
                householdId, choreId,
                dto.DisplayName, dto.Description,
                frequency, dto.IsOptional,
                dto.StartDate, dto.IsRequired,
                dto.MissedDeduction);

            return await handler.HandleAsync(command);
        });
    }
}
