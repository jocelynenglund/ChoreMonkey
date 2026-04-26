using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Security;

public interface IHouseholdPrincipalAccessor
{
    HouseholdPrincipal? Current { get; }
}

internal sealed class HouseholdPrincipalAccessor(IHttpContextAccessor httpContextAccessor) : IHouseholdPrincipalAccessor
{
    internal const string ItemKey = "ChoreMonkey.HouseholdPrincipal";

    public HouseholdPrincipal? Current =>
        httpContextAccessor.HttpContext?.Items[ItemKey] as HouseholdPrincipal;
}
