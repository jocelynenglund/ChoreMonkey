using ItsyBIT.Decisions;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Members.Commands.RemoveMember;

public record RemoveMemberCommand(
    Guid HouseholdId,
    Guid MemberId,
    Guid RemovedByMemberId,
    string PinCode);

public record RemoveMemberRequest(Guid RemovedByMemberId);

internal class Handler(IEventStore store)
{
    public async Task<(bool Success, string? Error)> HandleAsync(RemoveMemberCommand command)
    {
        var streamId = HouseholdAggregate.StreamId(command.HouseholdId);
        var history = (await store.FetchEventsAsync(streamId)).Cast<object>().ToList();

        // The admin PIN is verified here (an unparsable PIN is simply not verified).
        var verified = int.TryParse(command.PinCode, out var pinInt) && HouseholdPins.AdminPinMatches(history, pinInt);

        var decision = RemoveMemberSlice.Decide(history, command, verified);
        if (!decision.Accepted) return (false, decision.Error);

        foreach (var e in decision.Events)
            await store.AppendToStreamAsync(streamId, (IStoreableEvent)e, ExpectedVersion.Any);

        return (true, null);
    }
}

internal static class RemoveMemberEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/members/{memberId:guid}/remove", async (
            Guid householdId,
            Guid memberId,
            RemoveMemberRequest request,
            HttpContext httpContext,
            Handler handler) =>
        {
            var pinCode = httpContext.Request.Headers["X-Pin-Code"].FirstOrDefault();
            if (string.IsNullOrEmpty(pinCode))
                return Results.Unauthorized();
            
            var command = new RemoveMemberCommand(
                householdId,
                memberId,
                request.RemovedByMemberId,
                pinCode);
            
            var (success, error) = await handler.HandleAsync(command);
            
            if (!success)
            {
                return error switch
                {
                    "Invalid admin PIN" => Results.Json(new { error }, statusCode: StatusCodes.Status403Forbidden),
                    "Household not found" => Results.NotFound(),
                    "Member not found" => Results.NotFound(),
                    _ => Results.BadRequest(new { error })
                };
            }
            
            return Results.Ok();
        });
    }
}
