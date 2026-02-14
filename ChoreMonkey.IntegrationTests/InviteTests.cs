namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class InviteTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task GenerateInvite_ReturnsInviteLink()
    {
        // Arrange
        var household = await CreateHousehold("Invite Test Family");

        // Act
        var response = await _client.PostAsync($"/api/households/{household.HouseholdId}/invite", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<GenerateInviteResponse>();
        result.Should().NotBeNull();
        result!.InviteId.Should().NotBeEmpty();
        result.HouseholdId.Should().Be(household.HouseholdId);
        result.Link.Should().Contain(household.HouseholdId.ToString());
        result.Link.Should().Contain(result.InviteId.ToString());
    }

    [Fact]
    public async Task JoinWithValidInvite_BecomesMember()
    {
        // Arrange
        var household = await CreateHousehold("Join Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var joinRequest = new { InviteId = invite.InviteId, Nickname = "Kid1" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/join", 
            joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<JoinHouseholdResponse>();
        result.Should().NotBeNull();
        result!.MemberId.Should().NotBeEmpty();
        result.HouseholdId.Should().Be(household.HouseholdId);
        result.Nickname.Should().Be("Kid1");
    }

    [Fact]
    public async Task JoinWithInvalidInvite_ReturnsBadRequest()
    {
        // Arrange
        var household = await CreateHousehold("Invalid Invite Family");
        var fakeInviteId = Guid.NewGuid();
        var joinRequest = new { InviteId = fakeInviteId, Nickname = "Intruder" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/join", 
            joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinedMember_AppearsInMemberList()
    {
        // Arrange
        var household = await CreateHousehold("Member List Family");
        var invite = await GenerateInvite(household.HouseholdId);
        await JoinHousehold(household.HouseholdId, invite.InviteId, "ChildA");

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ListMembersResponse>();
        result!.Members.Should().HaveCount(2); // Owner + ChildA
        result.Members.Should().Contain(m => m.Nickname == "Admin"); // Default owner nickname
        result.Members.Should().Contain(m => m.Nickname == "ChildA");
    }

    #region Helpers

    private async Task<CreateHouseholdResponse> CreateHousehold(string name)
    {
        var request = new { Name = name, PinCode = 1234 };
        var response = await _client.PostAsJsonAsync("/api/households", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>())!;
    }

    private async Task<GenerateInviteResponse> GenerateInvite(Guid householdId)
    {
        var response = await _client.PostAsync($"/api/households/{householdId}/invite", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GenerateInviteResponse>())!;
    }

    private async Task<JoinHouseholdResponse> JoinHousehold(Guid householdId, Guid inviteId, string nickname)
    {
        var request = new { InviteId = inviteId, Nickname = nickname };
        var response = await _client.PostAsJsonAsync($"/api/households/{householdId}/join", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JoinHouseholdResponse>())!;
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record ListMembersResponse(List<MemberDto> Members);
    private record MemberDto(Guid MemberId, string Nickname);

    #endregion
}
