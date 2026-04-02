using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Security;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Salary.Commands.ConfigurePayday;

public record ConfigurePaydayCommand(Guid HouseholdId, int PaydayDayOfMonth, int PinCode);

public record ConfigurePaydayRequest(int PaydayDayOfMonth, int PinCode);

public record ConfigurePaydayResponse(int PaydayDayOfMonth);

internal class Handler(IEventStore store)
{
    public async Task<(bool IsAdmin, ConfigurePaydayResponse? Response)> HandleAsync(ConfigurePaydayCommand request)
    {
        if (request.PaydayDayOfMonth < 1 || request.PaydayDayOfMonth > 28)
        {
            throw new ArgumentException("PaydayDayOfMonth must be between 1 and 28.");
        }

        // Verify admin PIN
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var householdCreated = householdEvents.OfType<HouseholdCreated>().FirstOrDefault();

        if (householdCreated == null)
            return (false, null);

        var adminPinChanged = householdEvents.OfType<AdminPinChanged>().LastOrDefault();
        var currentAdminPinHash = adminPinChanged?.NewPinHash ?? householdCreated.PinHash;

        var isAdmin = PinHasher.VerifyPin(request.PinCode, currentAdminPinHash);
        if (!isAdmin)
            return (false, null);

        // Write event
        var salaryStreamId = SalaryAggregate.StreamId(request.HouseholdId);
        var evt = new PaydayConfigured(request.HouseholdId, request.PaydayDayOfMonth, DateTime.UtcNow);
        await store.AppendToStreamAsync(salaryStreamId, evt, ExpectedVersion.Any);

        return (true, new ConfigurePaydayResponse(request.PaydayDayOfMonth));
    }
}

internal static class ConfigurePaydayEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPut("households/{householdId:guid}/salary/payday", async (
            Guid householdId,
            ConfigurePaydayRequest request,
            Handler handler) =>
        {
            var command = new ConfigurePaydayCommand(householdId, request.PaydayDayOfMonth, request.PinCode);

            try
            {
                var (isAdmin, response) = await handler.HandleAsync(command);
                if (!isAdmin)
                    return Results.Unauthorized();
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}
