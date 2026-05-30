using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Feature.Chores.Commands.AddChore;

public record AddChoreCommand(
    Guid HouseholdId,
    Guid ChoreId,
    string DisplayName,
    string Description,
    ChoreFrequency? Frequency = null,
    bool IsOptional = false,
    DateTime? StartDate = null,
    bool IsRequired = true,
    decimal MissedDeduction = 10m,
    decimal? DeductionRate = null,
    decimal? BonusRate = null);

public record AddChoreRequest(
    string DisplayName,
    string Description,
    FrequencyRequest? Frequency = null,
    bool IsOptional = false,
    DateTime? StartDate = null,
    bool IsRequired = true,
    decimal MissedDeduction = 10m,
    decimal? DeductionRate = null,
    decimal? BonusRate = null);

public record FrequencyRequest(
    string Type,
    string[]? Days = null,
    int? IntervalDays = null);

public record AddChoreResponse(Guid Id);

internal class Handler(IEventStore store)
{
    public async Task<AddChoreResponse> HandleAsync(AddChoreCommand request)
    {
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);

        // Default to "once" if no frequency specified
        var frequency = request.Frequency ?? new ChoreFrequency("once");

        var choreCreated = new ChoreCreated(
            request.ChoreId,
            request.HouseholdId,
            request.DisplayName,
            request.Description,
            frequency,
            request.IsOptional,
            request.StartDate,
            request.IsRequired,
            request.MissedDeduction);

        await store.AppendToStreamAsync(choreStreamId, choreCreated, ExpectedVersion.Any);

        // Atomic-from-the-client's-view rates write. Folded in to prevent the
        // silent-drop bug where a separate /rates POST could fail and leave a
        // bonus chore with no BonusRate (then GetCurrentPeriod falls back to 0).
        if (request.DeductionRate.HasValue || request.BonusRate.HasValue)
        {
            var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);
            var ratesEvent = new ChoreRatesSet(
                request.HouseholdId,
                request.ChoreId,
                request.DeductionRate,
                request.BonusRate,
                DateTime.UtcNow);
            await store.AppendToStreamAsync(salaryStreamId, ratesEvent, ExpectedVersion.Any);
        }

        return new AddChoreResponse(request.ChoreId);
    }
}

internal static class AddChoreEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/chores", async (Guid householdId, AddChoreRequest dto, Feature.Chores.Commands.AddChore.Handler handler) =>
        {
            var frequency = dto.Frequency != null
                ? new ChoreFrequency(dto.Frequency.Type, dto.Frequency.Days, dto.Frequency.IntervalDays)
                : null;

            var choreId = Guid.NewGuid();
            var command = new AddChoreCommand(
                householdId,
                choreId,
                dto.DisplayName,
                dto.Description,
                frequency,
                dto.IsOptional,
                dto.StartDate,
                dto.IsRequired,
                dto.MissedDeduction,
                dto.DeductionRate,
                dto.BonusRate);

            var result = await handler.HandleAsync(command);
            return Results.Created($"/api/households/{householdId}/chores/{choreId}", result);
        });
    }
}
