namespace ChoreMonkey.IntegrationTests.Chores;

[Collection(nameof(ApiCollection))]
public class ChoreCompletionTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task CompleteChore_RecordsCompletion()
    {
        // Arrange
        var household = await CreateHousehold("Completion Test Family");
        await AddChore(household.HouseholdId, "Clean Room", "Tidy up");
        var chores = await GetChores(household.HouseholdId);
        var choreId = chores.Chores[0].ChoreId;

        // Act
        var completeRequest = new { MemberId = household.MemberId };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CompleteChoreResponse>();
        result!.ChoreId.Should().Be(choreId);
        result.CompletedBy.Should().Be(household.MemberId);
        result.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteChore_Retroactively_UsesProvidedDate()
    {
        // Arrange
        var household = await CreateHousehold("Retroactive Family");
        await AddChore(household.HouseholdId, "Wash Dishes", "Kitchen duty");
        var chores = await GetChores(household.HouseholdId);
        var choreId = chores.Chores[0].ChoreId;
        var yesterday = DateTime.UtcNow.AddDays(-1);

        // Act
        var completeRequest = new { MemberId = household.MemberId, CompletedAt = yesterday };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CompleteChoreResponse>();
        result!.CompletedAt.Should().BeCloseTo(yesterday, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetChoreHistory_ReturnsCompletions()
    {
        // Arrange
        var household = await CreateHousehold("History Family");
        await AddChore(household.HouseholdId, "Daily Task", "Repeat");
        var chores = await GetChores(household.HouseholdId);
        var choreId = chores.Chores[0].ChoreId;

        // Complete it twice
        await CompleteChore(household.HouseholdId, choreId, household.MemberId);
        await Task.Delay(100); // Small delay to ensure different timestamps
        await CompleteChore(household.HouseholdId, choreId, household.MemberId);

        // Act
        var response = await _client.GetAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var history = await response.Content.ReadFromJsonAsync<ChoreHistoryResponse>();
        history!.Completions.Should().HaveCount(2);
        history.Completions.Should().AllSatisfy(c => c.CompletedBy.Should().Be(household.MemberId));
    }

    [Fact]
    public async Task CompleteChore_UpdatesLastCompletedInChoreList()
    {
        // Arrange
        var household = await CreateHousehold("Last Completed Family");
        await AddChore(household.HouseholdId, "Check Mail", "Daily");
        var chores = await GetChores(household.HouseholdId);
        var choreId = chores.Chores[0].ChoreId;

        // Act
        await CompleteChore(household.HouseholdId, choreId, household.MemberId);
        
        // Assert
        var updatedChores = await GetChores(household.HouseholdId);
        updatedChores.Chores[0].LastCompletedAt.Should().NotBeNull();
        updatedChores.Chores[0].LastCompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        updatedChores.Chores[0].LastCompletedBy.Should().Be(household.MemberId);
    }

    #region Helpers

    private async Task<CreateHouseholdResponse> CreateHousehold(string name)
    {
        var request = new { Name = name, PinCode = 1234 };
        var response = await _client.PostAsJsonAsync("/api/households", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>())!;
    }

    private async Task AddChore(Guid householdId, string displayName, string description)
    {
        var request = new { DisplayName = displayName, Description = description };
        var response = await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GetChoresResponse> GetChores(Guid householdId)
    {
        return (await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{householdId}/chores"))!;
    }

    private async Task CompleteChore(Guid householdId, Guid choreId, Guid memberId)
    {
        var request = new { MemberId = memberId };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/complete", request);
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(
        Guid ChoreId, 
        string DisplayName, 
        string Description, 
        Guid[]? AssignedTo,
        DateTime? LastCompletedAt,
        Guid? LastCompletedBy);
    private record CompleteChoreResponse(Guid ChoreId, Guid CompletedBy, DateTime CompletedAt);
    private record ChoreHistoryResponse(List<CompletionDto> Completions);
    private record CompletionDto(Guid CompletedBy, DateTime CompletedAt);

    #endregion
}
