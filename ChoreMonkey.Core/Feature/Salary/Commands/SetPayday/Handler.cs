using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetPayday;

public record SetPaydayCommand(Guid HouseholdId, int PaydayDayOfMonth);

public record SetPaydayRequest(int PaydayDayOfMonth);

public record SetPaydayResponse(int PaydayDayOfMonth);

internal class Handler(IEventStore store)
{
    public async Task<SetPaydayResponse> HandleAsync(SetPaydayCommand request)
    {
        if (request.PaydayDayOfMonth < 1 || request.PaydayDayOfMonth > 28)
            throw new ArgumentOutOfRangeException(nameof(request.PaydayDayOfMonth), "Payday must be between 1 and 28.");

        var streamId = SalaryAggregate.StreamId(request.HouseholdId);

        var paydayEvent = new PaydayConfigured(request.HouseholdId, request.PaydayDayOfMonth);
        await store.AppendToStreamAsync(streamId, paydayEvent, ExpectedVersion.Any);

        return new SetPaydayResponse(request.PaydayDayOfMonth);
    }
}

internal static class SetPaydayEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/payday", async (
            Guid householdId,
            SetPaydayRequest request,
            Handler handler) =>
        {
            if (request.PaydayDayOfMonth < 1 || request.PaydayDayOfMonth > 28)
                return Results.BadRequest("PaydayDayOfMonth must be between 1 and 28.");

            var command = new SetPaydayCommand(householdId, request.PaydayDayOfMonth);
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
