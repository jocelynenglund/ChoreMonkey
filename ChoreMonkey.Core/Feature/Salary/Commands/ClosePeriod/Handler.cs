using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.ClosePeriod;

public record ClosePeriodCommand(Guid HouseholdId, DateTime? TargetPeriodEnd = null);

public record ClosePeriodRequest(DateTime? PeriodEnd = null);

public record MissedChoreDto(Guid ChoreId, string ChoreName, string Period, decimal Deduction);
public record BonusChoreDto(Guid ChoreId, string ChoreName, DateTime CompletedAt, decimal Bonus);

public record PayoutSummaryDto(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal Deductions,
    decimal Bonuses,
    decimal NetPay,
    List<MissedChoreDto> MissedChores,
    List<BonusChoreDto> BonusChores);

public record ClosePeriodResponse(
    Guid PeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<PayoutSummaryDto> Payouts);

internal class Handler(IEventStore store)
{
    public async Task<IResult> HandleAsync(ClosePeriodCommand request)
    {
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);

        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(salaryStreamId)).Cast<object>())
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();

        var outcome = ClosePeriodSlice.Close(history, request, DateTime.UtcNow);
        if (!outcome.Decision.Accepted)
            return Results.BadRequest(new { error = outcome.Decision.Error });

        foreach (var e in outcome.Decision.Events)
            await store.AppendToStreamAsync(salaryStreamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return Results.Ok(outcome.Response);
    }
}

internal static class ClosePeriodEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/close-period", async (
            Guid householdId,
            ClosePeriodRequest request,
            Handler handler) =>
        {
            var command = new ClosePeriodCommand(householdId, request.PeriodEnd);
            return await handler.HandleAsync(command);
        });
    }
}
