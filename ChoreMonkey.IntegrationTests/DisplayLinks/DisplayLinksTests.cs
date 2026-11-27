using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FileEventStore;
using FileEventStore.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChoreMonkey.IntegrationTests.DisplayLinks;

public class DisplayLinksTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(string HouseholdId, string AdminId)> CreateHouseholdAsync(string name = "The Smiths", string owner = "Mum")
    {
        var response = await _client.PostAsJsonAsync("/api/households", new { name, pinCode = 1234, ownerNickname = owner });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("householdId").GetString()!, body.GetProperty("memberId").GetString()!);
    }

    private HttpRequestMessage Admin(HttpMethod method, string url, object? body = null, string pin = "1234")
    {
        var request = new HttpRequestMessage(method, url);
        if (pin.Length > 0) request.Headers.Add("X-Pin-Code", pin);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<JsonElement> MakeLinkAsync(string householdId, string? label = "barrio")
    {
        var response = await _client.SendAsync(Admin(HttpMethod.Post, $"/api/households/{householdId}/display-links", new { label }));
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"make link: {response.StatusCode} {raw}");
        return JsonDocument.Parse(raw).RootElement;
    }

    private static string TokenOf(JsonElement link) => link.GetProperty("url").GetString()!.Split("/display/")[1];
    private static string PathOf(JsonElement link) => "/display/" + TokenOf(link);

    private async Task<string> AddChoreAsync(string householdId, string name, string? assignTo = null, bool toAll = false)
    {
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new { displayName = name, description = name, frequency = new { type = "daily" } });
        var chores = await _client.GetFromJsonAsync<JsonElement>($"/api/households/{householdId}/chores");
        var choreId = chores.GetProperty("chores").EnumerateArray().First(c => c.GetProperty("displayName").GetString() == name).GetProperty("choreId").GetString()!;
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign",
            toAll ? new { assignToAll = true } : (object)new { memberIds = new[] { assignTo } });
        return choreId;
    }

    // ── making, seeing, copying, revoking ────────────────────────────────────

    [Fact]
    public async Task An_admin_makes_a_link_and_can_copy_it_again_any_time()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId, "kitchen screen");

        var url = made.GetProperty("url").GetString()!;
        Assert.StartsWith("https://itsybitsylist-api.azurewebsites.net/display/", url);
        Assert.Matches(@"^[A-Za-z0-9_-]{43,}$", TokenOf(made));
        Assert.Equal("kitchen screen", made.GetProperty("label").GetString());

        var list = await _client.SendAsync(Admin(HttpMethod.Get, $"/api/households/{householdId}/display-links"));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var links = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("links").EnumerateArray().ToList();
        var only = Assert.Single(links);
        Assert.Equal(url, only.GetProperty("url").GetString());
        Assert.Equal(made.GetProperty("linkId").GetString(), only.GetProperty("linkId").GetString());
    }

    [Fact]
    public async Task Two_links_have_different_secrets()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        Assert.NotEqual(TokenOf(await MakeLinkAsync(householdId)), TokenOf(await MakeLinkAsync(householdId)));
    }

    [Fact]
    public async Task Making_a_link_needs_the_admin_pin()
    {
        var (householdId, _) = await CreateHouseholdAsync();

        var none = await _client.SendAsync(Admin(HttpMethod.Post, $"/api/households/{householdId}/display-links", new { label = "x" }, pin: ""));
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);

        var wrong = await _client.SendAsync(Admin(HttpMethod.Post, $"/api/households/{householdId}/display-links", new { label = "x" }, pin: "9999"));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

        var list = await _client.SendAsync(Admin(HttpMethod.Get, $"/api/households/{householdId}/display-links", pin: "9999"));
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Another_households_admin_cannot_see_or_revoke_the_link()
    {
        var (householdId, _) = await CreateHouseholdAsync("One");
        var (otherId, _) = await CreateHouseholdAsync("Two");
        var made = await MakeLinkAsync(householdId);

        var list = await _client.SendAsync(Admin(HttpMethod.Get, $"/api/households/{otherId}/display-links"));
        var links = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("links");
        Assert.Equal(0, links.GetArrayLength());

        var revoke = await _client.SendAsync(Admin(HttpMethod.Delete, $"/api/households/{otherId}/display-links/{made.GetProperty("linkId").GetString()}"));
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(PathOf(made) + "/today")).StatusCode);
    }

    [Fact]
    public async Task A_revoked_link_stops_working_and_leaves_the_list()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);
        var path = PathOf(made);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path + "/today")).StatusCode);

        var revoke = await _client.SendAsync(Admin(HttpMethod.Delete, $"/api/households/{householdId}/display-links/{made.GetProperty("linkId").GetString()}"));
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        foreach (var suffix in new[] { "", "/today", "/lately" })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(path + suffix)).StatusCode);

        var list = await _client.SendAsync(Admin(HttpMethod.Get, $"/api/households/{householdId}/display-links"));
        Assert.Equal(0, (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("links").GetArrayLength());

        var again = await _client.SendAsync(Admin(HttpMethod.Delete, $"/api/households/{householdId}/display-links/{made.GetProperty("linkId").GetString()}"));
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Revoking_needs_the_admin_pin()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);
        var linkId = made.GetProperty("linkId").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(Admin(HttpMethod.Delete, $"/api/households/{householdId}/display-links/{linkId}", pin: ""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(Admin(HttpMethod.Delete, $"/api/households/{householdId}/display-links/{linkId}", pin: "9999"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(PathOf(made) + "/today")).StatusCode);
    }

    // ── what a connected screen sees ─────────────────────────────────────────

    [Fact]
    public async Task Unknown_or_malformed_tokens_are_404()
    {
        foreach (var path in new[] { "/display/not-a-real-token", "/display/" + new string('A', 43), "/display/" + new string('A', 43) + "/today", "/display/" + new string('A', 43) + "/lately", "/display/..%2F..%2Fsecret" })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Today_lists_the_chores_with_who_did_them_and_where_the_household_lives()
    {
        var (householdId, adminId) = await CreateHouseholdAsync("The Smiths", "Mum");
        var dishes = await AddChoreAsync(householdId, "Dishes", assignTo: adminId);
        await AddChoreAsync(householdId, "Bins", toAll: true);
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/complete", new { memberId = adminId });
        var made = await MakeLinkAsync(householdId);

        var response = await _client.GetAsync(PathOf(made) + "/today");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("The Smiths", body.GetProperty("householdName").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", body.GetProperty("day").GetString()!);
        Assert.Equal($"https://choremonkey.itsybit.se/household/{householdId}", body.GetProperty("householdUrl").GetString());

        var chores = body.GetProperty("chores").EnumerateArray().ToList();
        Assert.Equal(2, chores.Count);
        var bins = chores.Single(c => c.GetProperty("title").GetString() == "Bins");
        Assert.Equal("open", bins.GetProperty("state").GetString());
        var done = chores.Single(c => c.GetProperty("title").GetString() == "Dishes");
        Assert.Equal("done", done.GetProperty("state").GetString());
        Assert.Equal("Mum", done.GetProperty("by").GetString());
        Assert.Equal(dishes, done.GetProperty("choreId").GetString());
    }

    [Fact]
    public async Task The_household_url_uses_the_vanity_name_when_there_is_one()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var slug = "smiths-" + Guid.NewGuid().ToString("N")[..8];
        var set = await _client.SendAsync(Admin(HttpMethod.Put, $"/api/households/{householdId}/slug", new { slug }));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var made = await MakeLinkAsync(householdId);

        var body = await _client.GetFromJsonAsync<JsonElement>(PathOf(made) + "/today");
        Assert.Equal($"https://choremonkey.itsybit.se/h/{slug}", body.GetProperty("householdUrl").GetString());
    }

    [Fact]
    public async Task Lately_counts_each_persons_completions_over_forty_days()
    {
        var (householdId, adminId) = await CreateHouseholdAsync("The Smiths", "Mum");
        var dishes = await AddChoreAsync(householdId, "Dishes", assignTo: adminId);
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/complete", new { memberId = adminId });
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/complete", new { memberId = adminId, completedAt = DateTime.UtcNow.AddDays(-5) });
        var made = await MakeLinkAsync(householdId);

        var body = await _client.GetFromJsonAsync<JsonElement>(PathOf(made) + "/lately");
        Assert.Equal("The Smiths", body.GetProperty("householdName").GetString());
        var person = Assert.Single(body.GetProperty("people").EnumerateArray());
        Assert.Equal("Mum", person.GetProperty("name").GetString());
        Assert.Equal(2, person.GetProperty("total").GetInt32());
        var days = person.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(40, days.Count);
        Assert.Equal(body.GetProperty("to").GetString(), days[^1].GetProperty("day").GetString());
        Assert.Equal(body.GetProperty("from").GetString(), days[0].GetProperty("day").GetString());
    }

    [Fact]
    public async Task The_households_own_screen_gets_the_same_two_views_without_a_link()
    {
        var (householdId, adminId) = await CreateHouseholdAsync();
        await AddChoreAsync(householdId, "Dishes", assignTo: adminId);

        var today = await _client.GetAsync($"/api/households/{householdId}/display/today");
        Assert.Equal(HttpStatusCode.OK, today.StatusCode);
        Assert.Equal("Dishes", (await today.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("chores")[0].GetProperty("title").GetString());

        var lately = await _client.GetAsync($"/api/households/{householdId}/display/lately");
        Assert.Equal(HttpStatusCode.OK, lately.StatusCode);
        Assert.Equal(40, (await lately.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("people")[0].GetProperty("days").GetArrayLength());

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/households/{Guid.NewGuid()}/display/today")).StatusCode);
    }

    // ── the page ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_page_shows_today_and_the_forty_days_and_is_locked_down()
    {
        var (householdId, adminId) = await CreateHouseholdAsync("The <b>Smiths</b>", "Mum <script>alert(1)</script>");
        var dishes = await AddChoreAsync(householdId, "Dishes & <i>pots</i>", assignTo: adminId);
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/complete", new { memberId = adminId });
        var made = await MakeLinkAsync(householdId);

        var response = await _client.GetAsync(PathOf(made));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("chores</title>", html);
        Assert.Contains("What a connected screen (barrio) sees. No pay or money is ever shared.", html);
        Assert.Contains("The &lt;b&gt;Smiths&lt;/b&gt;", html);
        Assert.Contains("Dishes &amp; &lt;i&gt;pots&lt;/i&gt;", html);
        Assert.Contains("Mum &lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("The last 40 days", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", html);

        Assert.Contains("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task The_page_says_when_nothing_is_on_the_list()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);
        var html = await (await _client.GetAsync(PathOf(made))).Content.ReadAsStringAsync();
        Assert.Contains("Nothing on the list today.", html);
    }

    [Fact]
    public async Task The_json_answers_carry_the_same_privacy_headers()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);
        foreach (var suffix in new[] { "/today", "/lately" })
        {
            var response = await _client.GetAsync(PathOf(made) + suffix);
            Assert.Contains("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        }
    }

    // ── no money, ever ───────────────────────────────────────────────────────

    static readonly string[] MoneyWords = ["salary", "allowance", "rate", "pay", "bonus", "deduction", "money", "wage", "price", "cost", "earn"];

    [Fact]
    public async Task No_money_is_shared_in_the_json_or_the_page()
    {
        var (householdId, adminId) = await CreateHouseholdAsync();
        var dishes = await AddChoreAsync(householdId, "Dishes", assignTo: adminId);
        // The household does have money set up: a salary, a chore rate.
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/rates", new { deductionRate = 7m, bonusRate = 3m });
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{dishes}/complete", new { memberId = adminId });
        var made = await MakeLinkAsync(householdId);

        foreach (var suffix in new[] { "/today", "/lately" })
        {
            var json = await (await _client.GetAsync(PathOf(made) + suffix)).Content.ReadAsStringAsync();
            foreach (var name in FieldNames(JsonDocument.Parse(json).RootElement))
                foreach (var word in MoneyWords)
                    Assert.False(name.Contains(word, StringComparison.OrdinalIgnoreCase), $"field '{name}' sounds like money ('{word}')");
        }

        var html = await (await _client.GetAsync(PathOf(made))).Content.ReadAsStringAsync();
        // The one sentence that promises there is no money is the only place those words may appear.
        var visible = html.Replace("No pay or money is ever shared.", "");
        foreach (var word in MoneyWords)
            Assert.DoesNotMatch(new Regex($@"(?i)\b{word}\b"), visible);
    }

    private static IEnumerable<string> FieldNames(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
            {
                yield return p.Name;
                foreach (var inner in FieldNames(p.Value)) yield return inner;
            }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var item in e.EnumerateArray())
                foreach (var inner in FieldNames(item)) yield return inner;
    }

    // ── where the secret lives ───────────────────────────────────────────────

    [Fact]
    public async Task The_secret_is_in_no_event_and_is_sealed_on_disk()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);
        var token = TokenOf(made);

        var vault = Path.Combine(fixture.DataPath, "display-links");
        Assert.True(Directory.Exists(vault));

        foreach (var file in Directory.GetFiles(fixture.DataPath, "*", SearchOption.AllDirectories))
        {
            var text = await File.ReadAllTextAsync(file);
            Assert.False(text.Contains(token), $"the token is in the clear in {file}");
        }

        // The events say only that a link was made.
        var eventFiles = Directory.GetFiles(fixture.DataPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(vault) && File.ReadAllText(f).Contains("DisplayLinkCreated"));
        Assert.NotEmpty(eventFiles);
    }

    [Fact]
    public async Task A_link_still_works_after_the_app_restarts()
    {
        var (householdId, _) = await CreateHouseholdAsync();
        var made = await MakeLinkAsync(householdId);

        using var second = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(ApiFixture.DisplayConfig(fixture.DataPath)));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEventStore>();
                services.RemoveAll<IEventSessionFactory>();
                services.AddFileEventStore(fixture.DataPath);
            });
        });
        var client = second.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PathOf(made) + "/today")).StatusCode);

        var request = Admin(HttpMethod.Get, $"/api/households/{householdId}/display-links");
        var links = (await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("links");
        Assert.Equal(made.GetProperty("url").GetString(), links[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Without_a_seal_key_setting_a_key_is_made_once_in_the_vault_and_links_can_be_copied()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), $"choremonkey-nokey-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Display:SealKey"] = "", ["Display:StorePath"] = Path.Combine(dataPath, "display-links") }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IEventStore>();
                    services.RemoveAll<IEventSessionFactory>();
                    services.AddFileEventStore(dataPath);
                });
            });
            var client = factory.CreateClient();
            var created = await client.PostAsJsonAsync("/api/households", new { name = "No key", pinCode = 1234, ownerNickname = "A" });
            var householdId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("householdId").GetString();

            var response = await client.SendAsync(Admin(HttpMethod.Post, $"/api/households/{householdId}/display-links", new { label = "x" }));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            var made = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
            var keyFile = Path.Combine(dataPath, "display-links", "seal.key");
            Assert.True(File.Exists(keyFile));
            Assert.DoesNotContain(made.Split('/').Last(), await File.ReadAllTextAsync(keyFile));

            // The same key next time: the link can still be copied.
            var listed = await client.SendAsync(Admin(HttpMethod.Get, $"/api/households/{householdId}/display-links", null));
            Assert.Contains(made, await listed.Content.ReadAsStringAsync());
        }
        finally
        {
            try { Directory.Delete(dataPath, true); } catch { }
        }
    }
}
