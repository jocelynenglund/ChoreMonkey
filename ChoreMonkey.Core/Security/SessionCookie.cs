using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ChoreMonkey.Core.Security;

public static class SessionCookie
{
    public const string Name = "cm.session";
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    public static void Issue(
        HttpResponse response,
        HouseholdPrincipal principal,
        ISessionTokenService tokens,
        IHostEnvironment env,
        TimeSpan? lifetime = null)
    {
        var ttl = lifetime ?? DefaultLifetime;
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        var token = tokens.CreateToken(principal, expiresAt);

        // Frontend (Simply.com) and API (Azure) live on different hosts, so the cookie
        // must be SameSite=None + Secure to flow on cross-site requests. In Development
        // we relax to Lax so http://localhost works without TLS.
        var isProd = !env.IsDevelopment() && !env.IsEnvironment("Testing");

        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProd,
            SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        });
    }

    public static void Clear(HttpResponse response, IHostEnvironment env)
    {
        var isProd = !env.IsDevelopment() && !env.IsEnvironment("Testing");
        response.Cookies.Append(Name, "", new CookieOptions
        {
            HttpOnly = true,
            Secure = isProd,
            SameSite = isProd ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true,
        });
    }
}
