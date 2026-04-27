using ChoreMonkey.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.Auth.Queries.WhoAmI;

public record WhoAmIResponse(Guid HouseholdId, Guid? MemberId, bool IsAdmin);

internal static class WhoAmIEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("auth/whoami", (IHouseholdPrincipalAccessor accessor) =>
        {
            var principal = accessor.Current;
            if (principal is null) return Results.NoContent();
            return Results.Ok(new WhoAmIResponse(principal.HouseholdId, principal.MemberId, principal.IsAdmin));
        });
    }
}
