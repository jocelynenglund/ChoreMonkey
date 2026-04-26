using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ChoreMonkey.IntegrationTests.Auth;

public class SessionCookieTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AccessHousehold_WithAdminPin_IssuesSessionCookie()
    {
        var householdId = await CreateHouseholdAsync("Cookie Admin Test", pinCode: 1234);

        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/access",
            new { pinCode = 1234 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = setCookies.FirstOrDefault(c => c.StartsWith("cm.session="));
        Assert.NotNull(sessionCookie);
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", sessionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccessHousehold_WithBadPin_DoesNotIssueCookie()
    {
        var householdId = await CreateHouseholdAsync("Bad Pin Test", pinCode: 1234);

        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/access",
            new { pinCode = 9999 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookies)
                && cookies.Any(c => c.StartsWith("cm.session=")),
            "no session cookie should be issued on bad PIN");
    }

    [Fact]
    public async Task WhoAmI_WithCookie_ReturnsAdminPrincipal()
    {
        var householdId = await CreateHouseholdAsync("WhoAmI Admin", pinCode: 1234);

        var accessResponse = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/access",
            new { pinCode = 1234 });
        var cookie = ExtractSessionCookie(accessResponse);

        using var whoAmI = new HttpRequestMessage(HttpMethod.Get, "/api/auth/whoami");
        whoAmI.Headers.Add("Cookie", $"cm.session={cookie}");
        var whoAmIResponse = await _client.SendAsync(whoAmI);

        var raw = await whoAmIResponse.Content.ReadAsStringAsync();
        Assert.True(whoAmIResponse.StatusCode == HttpStatusCode.OK,
            $"whoami status={whoAmIResponse.StatusCode}, body={raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(householdId, body.GetProperty("householdId").GetGuid().ToString());
        Assert.True(body.GetProperty("isAdmin").GetBoolean());

        // memberId is allowed to be either an explicit JSON null (default
        // serializer behavior) or omitted entirely (if the host overrides
        // DefaultIgnoreCondition). Either is acceptable here — we just need
        // to assert there's no member id leaked.
        var memberIdKind = body.TryGetProperty("memberId", out var m) ? m.ValueKind : JsonValueKind.Undefined;
        Assert.True(
            memberIdKind == JsonValueKind.Null || memberIdKind == JsonValueKind.Undefined,
            $"expected memberId to be null or absent, got kind={memberIdKind}, body={raw}");
    }

    [Fact]
    public async Task WhoAmI_WithoutCookie_Returns204()
    {
        var response = await _client.GetAsync("/api/auth/whoami");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_WithTamperedCookie_Returns204()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/whoami");
        req.Headers.Add("Cookie", "cm.session=this-is-not-a-valid-protected-blob");
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task JoinHousehold_IssuesCookieWithMemberId()
    {
        var householdId = await CreateHouseholdAsync("Join Cookie Test", pinCode: 1234);

        // Generate an invite (admin-side). The invite endpoint takes no body —
        // existing tests use PostAsync(..., null), follow that convention so we
        // don't accidentally trip body-binding.
        var inviteResponse = await _client.PostAsync(
            $"/api/households/{householdId}/invite",
            content: null);
        var inviteRaw = await inviteResponse.Content.ReadAsStringAsync();
        Assert.True(inviteResponse.StatusCode == HttpStatusCode.OK,
            $"invite status={inviteResponse.StatusCode}, body={inviteRaw}");
        var invite = JsonDocument.Parse(inviteRaw).RootElement;
        var inviteId = invite.GetProperty("inviteId").GetGuid();

        var joinResponse = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/join",
            new { inviteId, nickname = "TestMember" });
        var joinRaw = await joinResponse.Content.ReadAsStringAsync();
        Assert.True(joinResponse.StatusCode == HttpStatusCode.OK,
            $"join status={joinResponse.StatusCode}, body={joinRaw}");
        var join = JsonDocument.Parse(joinRaw).RootElement;
        var memberId = join.GetProperty("memberId").GetGuid().ToString();

        var cookie = ExtractSessionCookie(joinResponse);

        using var whoAmI = new HttpRequestMessage(HttpMethod.Get, "/api/auth/whoami");
        whoAmI.Headers.Add("Cookie", $"cm.session={cookie}");
        var whoAmIResponse = await _client.SendAsync(whoAmI);

        var whoAmIRaw = await whoAmIResponse.Content.ReadAsStringAsync();
        Assert.True(whoAmIResponse.StatusCode == HttpStatusCode.OK,
            $"whoami status={whoAmIResponse.StatusCode}, body={whoAmIRaw}");
        var body = JsonDocument.Parse(whoAmIRaw).RootElement;
        Assert.Equal(householdId, body.GetProperty("householdId").GetGuid().ToString());
        Assert.False(body.GetProperty("isAdmin").GetBoolean());
        Assert.Equal(memberId, body.GetProperty("memberId").GetGuid().ToString());
    }

    private async Task<string> CreateHouseholdAsync(string name, int pinCode)
    {
        var response = await _client.PostAsJsonAsync("/api/households", new
        {
            name,
            pinCode,
            ownerNickname = "Owner",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("householdId").GetGuid().ToString();
    }

    private static string ExtractSessionCookie(HttpResponseMessage response)
    {
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        var sessionCookie = setCookies.First(c => c.StartsWith("cm.session="));
        // "cm.session=<token>; Path=/; ..." → take just <token>
        var firstSemi = sessionCookie.IndexOf(';');
        var assignment = firstSemi >= 0 ? sessionCookie[..firstSemi] : sessionCookie;
        return assignment["cm.session=".Length..];
    }
}
