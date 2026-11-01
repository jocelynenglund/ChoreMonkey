using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
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
        var decision = SetPaydaySlice.Decide([], request);
        if (!decision.Accepted)
            throw new ArgumentOutOfRangeException(nameof(request.PaydayDayOfMonth), decision.Error);

        var streamId = SalaryAggregate.StreamId(request.HouseholdId);
        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

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
