using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests;

public class WeeklyAnydayTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;

    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    [Fact]
    public async Task WeeklyAnyday_NotOverdue_WhenCompletedThisWeek()
    {
        // Arrange - create household and weekly-anyday chore
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Weekly Test",
            pinCode = 1234,
            ownerNickname = "Parent"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string memberId = household!.GetProperty("memberId").GetString()!;

        // Create weekly chore with NO specific days (= any day this week)
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Weekly Cleanup",
            description = "Do once per week",
            frequency = new { type = "weekly" } // No days = any day
        });

        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        string choreId = choresData!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        // Assign to member
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign", new
        {
            memberIds = new[] { memberId }
        });

        // Complete it this week (e.g., Monday)
        var thisMonday = GetMondayOfWeek(DateTime.UtcNow);
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/complete", new
        {
            memberId,
            completedAt = thisMonday.AddHours(10).ToString("O")
        });

        // Act - check overdue (requires admin PIN)
        var overdueRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/overdue");
        overdueRequest.Headers.Add("X-Pin-Code", "1234");
        var overdueResponse = await _client.SendAsync(overdueRequest);
        var overdueData = await overdueResponse.Content.ReadFromJsonAsync<dynamic>();

        // Assert - member should have 0 overdue chores
        var memberOverdue = overdueData!.GetProperty("memberOverdue");
        foreach (var member in memberOverdue.EnumerateArray())
        {
            if (member.GetProperty("memberId").GetString() == memberId)
            {
                Assert.Equal(0, member.GetProperty("overdueCount").GetInt32());
            }
        }
    }

    [Fact]
    public async Task WeeklyAnyday_ShowsCompletedThisWeek_InChoreList()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Weekly View Test",
            pinCode = 1234,
            ownerNickname = "Dad"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string memberId = household!.GetProperty("memberId").GetString()!;

        // Create weekly-anyday chore
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Mow Lawn",
            frequency = new { type = "weekly" }
        });

        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        string choreId = choresData!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        // Assign and complete this week
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign", new
        {
            memberIds = new[] { memberId }
        });
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/complete", new
        {
            memberId
        });

        // Act - get chore list with member context
        var listResponse = await _client.GetAsync($"/api/households/{householdId}/chores?memberId={memberId}");
        var listData = await listResponse.Content.ReadFromJsonAsync<dynamic>();

        // Assert - chore shows completedThisWeek for this member
        var chore = listData!.GetProperty("chores")[0];
        var memberCompletions = chore.GetProperty("memberCompletions");
        
        bool foundCompletion = false;
        foreach (var mc in memberCompletions.EnumerateArray())
        {
            if (mc.GetProperty("memberId").GetString() == memberId)
            {
                Assert.True(mc.GetProperty("completedThisWeek").GetBoolean());
                foundCompletion = true;
            }
        }
        Assert.True(foundCompletion, "Should find member completion");
    }

    [Fact]
    public async Task WeeklyAnyday_EachMemberTrackedSeparately()
    {
        // Arrange - household with 2 members
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Family Weekly Test",
            pinCode = 1234,
            ownerNickname = "Parent"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string parentId = household!.GetProperty("memberId").GetString()!;

        // Add second member via invite
        var inviteResponse = await _client.PostAsync($"/api/households/{householdId}/invite", null);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<dynamic>();
        string inviteId = invite!.GetProperty("inviteId").GetString()!;

        var joinResponse = await _client.PostAsJsonAsync($"/api/households/{householdId}/join", new
        {
            inviteId,
            nickname = "Kid"
        });
        var joinData = await joinResponse.Content.ReadFromJsonAsync<dynamic>();
        string kidId = joinData!.GetProperty("memberId").GetString()!;

        // Create weekly-anyday chore assigned to everyone
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Take Out Trash",
            frequency = new { type = "weekly" }
        });

        var choresResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var choresData = await choresResponse.Content.ReadFromJsonAsync<dynamic>();
        string choreId = choresData!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign", new
        {
            assignToAll = true
        });

        // Only Parent completes it
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/complete", new
        {
            memberId = parentId
        });

        // Act - get chore list
        var listResponse = await _client.GetAsync($"/api/households/{householdId}/chores");
        var listData = await listResponse.Content.ReadFromJsonAsync<dynamic>();

        // Assert - Parent completed, Kid didn't
        var chore = listData!.GetProperty("chores")[0];
        var memberCompletions = chore.GetProperty("memberCompletions");

        bool parentCompletedThisWeek = false;
        bool kidCompletedThisWeek = false;

        foreach (var mc in memberCompletions.EnumerateArray())
        {
            var id = mc.GetProperty("memberId").GetString();
            var completedThisWeek = mc.GetProperty("completedThisWeek").GetBoolean();
            
            if (id == parentId) parentCompletedThisWeek = completedThisWeek;
            if (id == kidId) kidCompletedThisWeek = completedThisWeek;
        }

        Assert.True(parentCompletedThisWeek, "Parent should show completed this week");
        Assert.False(kidCompletedThisWeek, "Kid should NOT show completed this week");
    }
}
