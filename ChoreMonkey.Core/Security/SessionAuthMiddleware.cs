using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ChoreMonkey.Core.Security;

internal sealed class SessionAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISessionTokenService tokens)
    {
        if (context.Request.Cookies.TryGetValue(SessionCookie.Name, out var token)
            && !string.IsNullOrWhiteSpace(token))
        {
            var principal = tokens.TryReadToken(token);
            if (principal is not null)
            {
                context.Items[HouseholdPrincipalAccessor.ItemKey] = principal;
            }
        }

        await next(context);
    }
}

public static class SessionAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseChoreMonkeyAuth(this IApplicationBuilder app)
        => app.UseMiddleware<SessionAuthMiddleware>();
}
