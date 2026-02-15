using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ChoreMonkey.IntegrationTests.Activity;

public class TeamOverviewTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Admin_can_view_team_overview_with_all_member_chores()
    {
        // Arrange: Create household with admin
        var createResponse = await fixture.Client.PostAsJsonAsync("/api/households", new
        {
            name = "Team House",
            pinCode = 1234,
            ownerNickname = "Parent"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string adminId = household.GetProperty("memberId").GetString()!;

        // Arrange: Add a second member via invite
        var inviteResponse = await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/invite", new { });
        var invite = await inviteResponse.Content.ReadFromJsonAsync<dynamic>();
        string inviteId = invite!.GetProperty("inviteId").GetString()!;

        var joinResponse = await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/join", new
        {
            inviteId,
            nickname = "Child"
        });
        var joined = await joinResponse.Content.ReadFromJsonAsync<dynamic>();
        string childId = joined!.GetProperty("memberId").GetString()!;

        // Arrange: Create a chore assigned to all
        await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Clean room",
            description = "Weekly cleaning",
            frequency = new { type = "weekly" }
        });
        var choresResponse = await fixture.Client.GetFromJsonAsync<dynamic>($"/api/households/{householdId}/chores");
        string choreId = choresResponse!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        // Assign to all
        await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign", new
        {
            assignToAll = true
        });

        // Complete for admin only
        await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/complete", new
        {
            memberId = adminId
        });

        // Act: Get team overview
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/team");
        request.Headers.Add("X-Pin-Code", "1234");
        var response = await fixture.Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        var members = result!.GetProperty("members");
        
        Assert.Equal(2, members.GetArrayLength());
        
        // Find parent and child
        dynamic? parentMember = null;
        dynamic? childMember = null;
        for (int i = 0; i < members.GetArrayLength(); i++)
        {
            var member = members[i];
            if (member.GetProperty("nickname").GetString() == "Parent")
                parentMember = member;
            else if (member.GetProperty("nickname").GetString() == "Child")
                childMember = member;
        }
        
        Assert.NotNull(parentMember);
        Assert.NotNull(childMember);
        
        // Parent completed the chore
        Assert.Equal(1, parentMember!.GetProperty("completedCount").GetInt32());
        Assert.Equal("completed", parentMember.GetProperty("chores")[0].GetProperty("status").GetString());
        
        // Child has not completed
        Assert.Equal(0, childMember!.GetProperty("completedCount").GetInt32());
        Assert.Equal("pending", childMember.GetProperty("chores")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Team_overview_requires_admin_pin()
    {
        // Arrange
        var createResponse = await fixture.Client.PostAsJsonAsync("/api/households", new
        {
            name = "Secure House",
            pinCode = 9999,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;

        // Act: Try with wrong PIN
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/team");
        request.Headers.Add("X-Pin-Code", "0000");
        var response = await fixture.Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Team_overview_shows_overdue_status_for_missed_chores()
    {
        // Arrange
        var createResponse = await fixture.Client.PostAsJsonAsync("/api/households", new
        {
            name = "Overdue House",
            pinCode = 5555,
            ownerNickname = "Admin"
        });
        var household = await createResponse.Content.ReadFromJsonAsync<dynamic>();
        string householdId = household!.GetProperty("householdId").GetString()!;
        string memberId = household.GetProperty("memberId").GetString()!;

        // Create a daily chore with start date in the past
        var yesterday = DateTime.UtcNow.AddDays(-2).ToString("O");
        await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/chores", new
        {
            displayName = "Daily task",
            description = "Should be overdue",
            frequency = new { type = "daily" },
            startDate = yesterday
        });
        var choresResponse = await fixture.Client.GetFromJsonAsync<dynamic>($"/api/households/{householdId}/chores");
        string choreId = choresResponse!.GetProperty("chores")[0].GetProperty("choreId").GetString()!;

        // Assign to admin
        await fixture.Client.PostAsJsonAsync($"/api/households/{householdId}/chores/{choreId}/assign", new
        {
            memberIds = new[] { memberId }
        });

        // Don't complete it - should be overdue

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/team");
        request.Headers.Add("X-Pin-Code", "5555");
        var response = await fixture.Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        var member = result!.GetProperty("members")[0];
        
        Assert.Equal(1, member.GetProperty("overdueCount").GetInt32());
        Assert.Equal("overdue", member.GetProperty("chores")[0].GetProperty("status").GetString());
    }
}
