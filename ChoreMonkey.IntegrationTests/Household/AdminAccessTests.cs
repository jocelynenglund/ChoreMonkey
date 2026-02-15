using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests.Household;

public class AdminAccessTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AccessWithAdminPin_ReturnsIsAdminTrue()
    {
        // Create household with admin PIN 1234
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Admin Test",
            pinCode = 1234,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Access with admin PIN
        var accessResponse = await _client.PostAsJsonAsync($"/api/households/{householdId}/access", new
        {
            pinCode = 1234
        });
        var access = await accessResponse.Content.ReadFromJsonAsync<dynamic>();

        Assert.True(access!.GetProperty("success").GetBoolean());
        Assert.True(access!.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task AccessWithMemberPin_ReturnsIsAdminFalse()
    {
        // Create household with admin PIN 1234 and member PIN 0000
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Member Test",
            pinCode = 1234,
            memberPinCode = 0000,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Access with member PIN
        var accessResponse = await _client.PostAsJsonAsync($"/api/households/{householdId}/access", new
        {
            pinCode = 0000
        });
        var access = await accessResponse.Content.ReadFromJsonAsync<dynamic>();

        Assert.True(access!.GetProperty("success").GetBoolean());
        Assert.False(access!.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task LegacyHousehold_AdminPinEqualsRegularPin()
    {
        // Create household without member PIN (legacy behavior)
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Legacy Test",
            pinCode = 1234,
            ownerNickname = "Owner"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Access with the PIN - should be admin (backward compatible)
        var accessResponse = await _client.PostAsJsonAsync($"/api/households/{householdId}/access", new
        {
            pinCode = 1234
        });
        var access = await accessResponse.Content.ReadFromJsonAsync<dynamic>();

        Assert.True(access!.GetProperty("success").GetBoolean());
        Assert.True(access!.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task DeleteChore_RequiresAdminAccess()
    {
        // Create household with separate PINs
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Delete Test",
            pinCode = 1234,
            memberPinCode = 5555,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Create a chore
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Test Chore",
            description = "To be deleted"
        });

        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        string choreId = choresData!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        // Try delete with member PIN → 403 Forbidden
        var deleteResponse1 = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/delete",
            new { pinCode = 5555 });
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse1.StatusCode);

        // Delete with admin PIN → 200 OK
        var deleteResponse2 = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/delete",
            new { pinCode = 1234 });
        Assert.Equal(HttpStatusCode.OK, deleteResponse2.StatusCode);

        // Verify chore is deleted
        var choresAfter = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresDataAfter = await choresAfter.Content.ReadFromJsonAsync<dynamic>();
        Assert.Equal(0, choresDataAfter!.GetProperty("chores").GetArrayLength());
    }

    [Fact]
    public async Task AdminCanChangeAdminPin()
    {
        // Create household
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Change PIN Test",
            pinCode = 1234,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Change admin PIN from 1234 to 9999
        var changeResponse = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/admin-pin",
            new { currentPinCode = 1234, newPinCode = 9999 });
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        // Old PIN no longer works (no member PIN set, so it's completely invalid)
        var access1 = await _client.PostAsJsonAsync($"/api/households/{householdId}/access", new
        {
            pinCode = 1234
        });
        Assert.Equal(HttpStatusCode.Unauthorized, access1.StatusCode);

        // New PIN is admin
        var access2 = await _client.PostAsJsonAsync($"/api/households/{householdId}/access", new
        {
            pinCode = 9999
        });
        var access2Data = await access2.Content.ReadFromJsonAsync<dynamic>();
        Assert.True(access2Data!.GetProperty("success").GetBoolean());
        Assert.True(access2Data!.GetProperty("isAdmin").GetBoolean());
    }
}
