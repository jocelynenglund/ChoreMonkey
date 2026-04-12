using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
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
        var events = await store.FetchEventsAsync(streamId);

        // Verify chore exists and belongs to this household
        var exists = events.OfType<ChoreCreated>().Any(e => e.ChoreId == request.ChoreId);
        var deleted = events.OfType<ChoreDeleted>().Any(e => e.ChoreId == request.ChoreId);
        if (!exists || deleted)
            return Results.NotFound(new { error = "Chore not found." });

        var updated = new ChoreUpdated(
            request.ChoreId,
            request.HouseholdId,
            request.DisplayName,
            request.Description,
            request.Frequency,
            request.IsOptional,
            request.StartDate,
            request.IsRequired,
            request.MissedDeduction);

        await store.AppendToStreamAsync(streamId, updated, ExpectedVersion.Any);
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
