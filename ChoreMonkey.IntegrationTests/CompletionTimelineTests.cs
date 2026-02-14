using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests;

public class CompletionTimelineTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task GetCompletionTimeline_ReturnsEmptyWhenNoCompletions()
    {
        // Arrange - create household
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Timeline Test",
            pinCode = 1234,
            ownerNickname = "Owner"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Act
        var response = await _client.GetAsync($"/api/households/{householdId}/completions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        var completions = result!.GetProperty("completions");
        Assert.Equal(0, completions.GetArrayLength());
    }

    [Fact]
    public async Task GetCompletionTimeline_ReturnsCompletionsInOrder()
    {
        // Arrange - create household and chores
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Timeline Test 2",
            pinCode = 1234,
            ownerNickname = "Dad"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string memberId = household!.GetProperty("memberId").GetString()!;

        // Create two chores
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Chore A",
            description = "First chore"
        });
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Chore B",
            description = "Second chore"
        });

        // Get chore IDs
        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        var chores = choresData!.GetProperty("chores");
        string choreAId = chores[0].GetProperty("choreId").GetString()!;
        string choreBId = chores[1].GetProperty("choreId").GetString()!;

        // Complete both chores
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreAId}/complete", new
        {
            memberId = memberId
        });
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreBId}/complete", new
        {
            memberId = memberId
        });

        // Act
        var response = await _client.GetAsync($"/api/households/{householdId}/completions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        var completions = result!.GetProperty("completions");
        Assert.Equal(2, completions.GetArrayLength());
        
        // Most recent should be first (Chore B)
        Assert.Equal("Chore B", completions[0].GetProperty("choreName").GetString());
        Assert.Equal("Dad", completions[0].GetProperty("completedByNickname").GetString());
    }

    [Fact]
    public async Task GetCompletionTimeline_RespectsLimitParameter()
    {
        // Arrange - create household with many completions
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Timeline Limit Test",
            pinCode = 1234,
            ownerNickname = "Parent"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string memberId = household!.GetProperty("memberId").GetString()!;

        // Create and complete 5 chores
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
            {
                displayName = $"Task {i}",
                description = "Test"
            });
        }

        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        var chores = choresData!.GetProperty("chores");
        
        for (int i = 0; i < 5; i++)
        {
            string choreId = chores[i].GetProperty("choreId").GetString()!;
            await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/complete", new
            {
                memberId = memberId
            });
        }

        // Act - request only 3
        var response = await _client.GetAsync($"/api/households/{householdId}/completions?limit=3");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        var completions = result!.GetProperty("completions");
        Assert.Equal(3, completions.GetArrayLength());
    }
}
