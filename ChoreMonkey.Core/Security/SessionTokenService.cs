using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ChoreMonkey.Core.Security;

public interface ISessionTokenService
{
    string CreateToken(HouseholdPrincipal principal, DateTimeOffset expiresAt);
    HouseholdPrincipal? TryReadToken(string token);
}

public sealed class SessionTokenService : ISessionTokenService
{
    private const string ProtectorPurpose = "ChoreMonkey.Session.v1";
    private readonly IDataProtector _protector;

    public SessionTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);
    }

    public string CreateToken(HouseholdPrincipal principal, DateTimeOffset expiresAt)
    {
        var payload = new TokenPayload(
            principal.HouseholdId,
            principal.MemberId,
            principal.IsAdmin,
            expiresAt.ToUnixTimeSeconds());

        var json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json);
    }

    public HouseholdPrincipal? TryReadToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        TokenPayload payload;
        try
        {
            var json = _protector.Unprotect(token);
            payload = JsonSerializer.Deserialize<TokenPayload>(json)
                ?? throw new InvalidOperationException("empty payload");
        }
        catch
        {
            // Tampered, expired key, or malformed — treat as no session.
            return null;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(payload.Exp) <= DateTimeOffset.UtcNow)
            return null;

        return new HouseholdPrincipal(payload.HouseholdId, payload.MemberId, payload.IsAdmin);
    }

    private sealed record TokenPayload(Guid HouseholdId, Guid? MemberId, bool IsAdmin, long Exp);
}
