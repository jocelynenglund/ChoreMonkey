namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ChoreTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AddChore_ReturnsCreated()
    {
        // Arrange
        var household = await CreateHousehold("Chore Test Family");
        var choreRequest = new { DisplayName = "Clean Room", Description = "Make your bed and tidy up" };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores", 
            choreRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddChore_AppearsInList()
    {
        // Arrange
        var household = await CreateHousehold("Chore List Family");
        var choreRequest = new { DisplayName = "Do Dishes", Description = "Wash and dry all dishes" };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<GetChoresResponse>();
        result!.Chores.Should().HaveCount(1);
        result.Chores[0].DisplayName.Should().Be("Do Dishes");
        result.Chores[0].Description.Should().Be("Wash and dry all dishes");
        result.Chores[0].AssignedTo.Should().BeNull();
    }

    [Fact]
    public async Task AssignChore_UpdatesAssignment()
    {
        // Arrange
        var household = await CreateHousehold("Assignment Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Junior");
        
        // Add a chore
        var choreRequest = new { DisplayName = "Take Out Trash", Description = "Weekly trash duty" };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        // Get the chore ID
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;

        // Act - assign to single member using new API
        var assignRequest = new { MemberIds = new[] { kid.MemberId }, AssignToAll = false };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            assignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify assignment persisted
        var updatedChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        updatedChores!.Chores[0].AssignedTo.Should().Contain(kid.MemberId);
    }

    [Fact]
    public async Task GetChores_EmptyHousehold_ReturnsEmptyList()
    {
        // Arrange
        var household = await CreateHousehold("Empty Chores Family");

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<GetChoresResponse>();
        result!.Chores.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleChores_AllAppearInList()
    {
        // Arrange
        var household = await CreateHousehold("Multi Chore Family");
        
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", 
            new { DisplayName = "Chore 1", Description = "First" });
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", 
            new { DisplayName = "Chore 2", Description = "Second" });
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", 
            new { DisplayName = "Chore 3", Description = "Third" });

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetChoresResponse>();
        result!.Chores.Should().HaveCount(3);
        result.Chores.Select(c => c.DisplayName).Should().BeEquivalentTo(["Chore 1", "Chore 2", "Chore 3"]);
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
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName, string Description, Guid[]? AssignedTo);

    #endregion
}
