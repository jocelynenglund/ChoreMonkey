using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests;

public class MemberProfileTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;
    private const int AdminPin = 1234;

    #region ChangeMemberNickname Tests

    [Fact]
    public async Task Member_can_change_their_nickname()
    {
        // Arrange
        var household = await CreateHousehold("Nickname Test Family");
        
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "NewNickname" });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<NicknameResponse>();
        Assert.Equal("NewNickname", result!.Nickname);
        
        // Verify nickname is updated in member list
        var membersResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/members");
        var members = await membersResponse.Content.ReadFromJsonAsync<MembersResponse>();
        Assert.Contains(members!.Members, m => m.Nickname == "NewNickname");
    }

    [Fact]
    public async Task Nickname_change_is_idempotent_for_same_value()
    {
        // Arrange
        var household = await CreateHousehold("Idempotent Test");
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "SameName" });
        
        // Act - Change to same name again
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "SameName" });
        
        // Assert - Should succeed without error
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_change_nickname_for_nonexistent_member()
    {
        // Arrange
        var household = await CreateHousehold("NonExistent Test");
        var fakeMemberId = Guid.NewGuid();
        
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{fakeMemberId}/nickname",
            new { nickname = "Whatever" });
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_change_nickname_to_empty_string()
    {
        // Arrange
        var household = await CreateHousehold("Empty Nickname Test");
        
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "" });
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nickname_change_reflects_in_team_overview()
    {
        // Arrange
        var household = await CreateHousehold("Team Overview Nickname Test");
        
        // Change nickname
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "UpdatedName" });
        
        // Act - Get team overview
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/households/{household.HouseholdId}/team");
        request.Headers.Add("X-Pin-Code", AdminPin.ToString());
        var response = await _client.SendAsync(request);
        
        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("UpdatedName", content);
    }

    #endregion

    #region ChangeMemberStatus Tests

    [Fact]
    public async Task Member_can_set_status()
    {
        // Arrange
        var household = await CreateHousehold("Status Test Family");
        
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/status",
            new { status = "Busy doing chores!" });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("Busy doing chores!", result!.Status);
    }

    [Fact]
    public async Task Member_can_clear_status()
    {
        // Arrange
        var household = await CreateHousehold("Clear Status Test");
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/status",
            new { status = "Some status" });
        
        // Act - Clear status by setting empty string
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/status",
            new { status = "" });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("", result!.Status);
    }

    [Fact]
    public async Task Status_appears_in_member_list()
    {
        // Arrange
        var household = await CreateHousehold("Status List Test");
        
        // Act
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/status",
            new { status = "Working from home" });
        
        // Assert
        var membersResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/members");
        var content = await membersResponse.Content.ReadAsStringAsync();
        Assert.Contains("Working from home", content);
    }

    [Fact]
    public async Task Cannot_set_status_for_nonexistent_member()
    {
        // Arrange
        var household = await CreateHousehold("NonExistent Status Test");
        var fakeMemberId = Guid.NewGuid();
        
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{fakeMemberId}/status",
            new { status = "Whatever" });
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Nickname in Activity Timeline Tests

    [Fact]
    public async Task Nickname_change_appears_in_activity_timeline()
    {
        // Arrange
        var household = await CreateHousehold("Timeline Nickname Test");
        
        // Act - Change nickname
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "NewTimelineName" });
        
        // Get timeline
        var timelineResponse = await _client.GetAsync(
            $"/api/households/{household.HouseholdId}/completions");
        
        // Assert
        var content = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains("changed name to", content);
        Assert.Contains("NewTimelineName", content);
    }

    [Fact]
    public async Task Chore_completion_shows_current_nickname_not_old()
    {
        // Arrange
        var household = await CreateHousehold("Completion Nickname Test");
        
        // Create a chore
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new { displayName = "Test Chore", description = "Test" });
        
        // Fetch chore list to get the ID
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<ChoresResponse>();
        var choreId = chores!.Chores.First().ChoreId;
        
        // Complete the chore
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { memberId = household.MemberId });
        
        // Change nickname AFTER completion
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/members/{household.MemberId}/nickname",
            new { nickname = "RenamedMember" });
        
        // Act - Get timeline
        var timelineResponse = await _client.GetAsync(
            $"/api/households/{household.HouseholdId}/completions");
        
        // Assert - Should show CURRENT nickname for completions
        var content = await timelineResponse.Content.ReadAsStringAsync();
        Assert.Contains("RenamedMember", content);
    }

    #endregion

    #region Helpers

    private async Task<HouseholdResponse> CreateHousehold(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/households", new
        {
            name,
            ownerNickname = "Owner",
            pinCode = AdminPin
        });
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private record HouseholdResponse(Guid HouseholdId, Guid MemberId);
    private record NicknameResponse(Guid MemberId, string Nickname);
    private record StatusResponse(Guid MemberId, string Status);
    private record MemberDto(Guid MemberId, string Nickname, string? Status);
    private record MembersResponse(List<MemberDto> Members);
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record ChoresResponse(List<ChoreDto> Chores);

    #endregion
}
