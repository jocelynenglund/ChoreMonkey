using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using ItsyBIT.Decisions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetMemberSalary;

public record SetMemberSalaryCommand(
    Guid HouseholdId,
    Guid MemberId,
    decimal BaseSalary,
    decimal DeductionMultiplier,
    decimal BonusMultiplier);

public record SetMemberSalaryRequest(
    decimal BaseSalary,
    decimal DeductionMultiplier,
    decimal BonusMultiplier);

public record SetMemberSalaryResponse(
    Guid MemberId,
    decimal BaseSalary,
    decimal DeductionMultiplier,
    decimal BonusMultiplier);

internal class Handler(IEventStore store)
{
    public async Task<SetMemberSalaryResponse> HandleAsync(SetMemberSalaryCommand request)
    {
        var streamId = SalaryAggregate.StreamId(request.HouseholdId);
        
        var decision = SetMemberSalarySlice.Decide([], request, DateTime.UtcNow);
        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);
        
        return new SetMemberSalaryResponse(
            request.MemberId,
            request.BaseSalary,
            request.DeductionMultiplier,
            request.BonusMultiplier);
    }
}

internal static class SetMemberSalaryEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/members/{memberId:guid}/salary", async (
            Guid householdId,
            Guid memberId,
            SetMemberSalaryRequest request,
            Handler handler) =>
        {
            var command = new SetMemberSalaryCommand(
                householdId,
                memberId,
                request.BaseSalary,
                request.DeductionMultiplier,
                request.BonusMultiplier);
                
            var result = await handler.HandleAsync(command);
            return Results.Ok(result);
        });
    }
}
