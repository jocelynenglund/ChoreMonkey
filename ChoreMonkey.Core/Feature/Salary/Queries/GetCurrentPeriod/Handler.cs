using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetCurrentPeriod;

public record GetCurrentPeriodQuery(Guid HouseholdId, Guid? MemberId = null);

public record MissedChoreDto(Guid ChoreId, string ChoreName, string Period, decimal Deduction);
public record BonusChoreDto(Guid ChoreId, string ChoreName, DateTime CompletedAt, decimal Bonus);

public record MemberPeriodSummary(
    Guid MemberId,
    string Name,
    decimal BaseSalary,
    decimal DeductionMultiplier,
    decimal BonusMultiplier,
    decimal Deductions,
    decimal Bonuses,
    decimal Projected,
    List<MissedChoreDto> MissedChores,
    List<BonusChoreDto> BonusChores);

public record CurrentPeriodResponse(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<MemberPeriodSummary> Members);

internal class Handler(IEventStore store)
{
    public async Task<CurrentPeriodResponse> HandleAsync(GetCurrentPeriodQuery request)
    {
        var history = (await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>()
            .Concat((await store.FetchEventsAsync(SalaryAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .Concat((await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId))).Cast<object>())
            .ToList();
        return CurrentPeriodView.Project(history, request.MemberId, DateTime.UtcNow);
    }
}

internal static class GetCurrentPeriodEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/salary/current", async (
            Guid householdId,
            Guid? memberId,
            Handler handler) =>
        {
            var query = new GetCurrentPeriodQuery(householdId, memberId);
            var result = await handler.HandleAsync(query);
            return Results.Ok(result);
        });
    }
}
