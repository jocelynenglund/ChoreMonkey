using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests.Activity;

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
        var activities = result!.GetProperty("activities");
        
        // New household will have "member_joined" activity
        // But no completions, which is what we're testing
        var completionCount = 0;
        for (int i = 0; i < activities.GetArrayLength(); i++)
        {
            if (activities[i].GetProperty("type").GetString() == "completion")
                completionCount++;
        }
        Assert.Equal(0, completionCount);
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
        var activities = result!.GetProperty("activities");
        
        // Filter to just completions
        var completions = new List<System.Text.Json.JsonElement>();
        for (int i = 0; i < activities.GetArrayLength(); i++)
        {
            if (activities[i].GetProperty("type").GetString() == "completion")
                completions.Add(activities[i]);
        }
        
        Assert.Equal(2, completions.Count);
        
        // Most recent should be first (Chore B) - check description contains names
        Assert.Contains("Chore B", completions[0].GetProperty("description").GetString());
        Assert.Contains("Dad", completions[0].GetProperty("description").GetString());
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
        var activities = result!.GetProperty("activities");
        
        // Should have exactly 3 activities total (limit=3)
        Assert.Equal(3, activities.GetArrayLength());
    }
}
