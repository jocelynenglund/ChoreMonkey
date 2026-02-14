namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class OptionalChoreTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AddOptionalChore_AppearsWithFlag()
    {
        // Arrange
        var household = await CreateHousehold("Bonus Test Family");
        var choreRequest = new
        {
            DisplayName = "Wash the Car",
            Description = "Bonus chore for extra credit",
            IsOptional = true
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].DisplayName.Should().Be("Wash the Car");
        chores.Chores[0].IsOptional.Should().BeTrue();
    }

    [Fact]
    public async Task OptionalChore_NeverAppearsInOverdue()
    {
        // Arrange
        var household = await CreateHousehold("Bonus Never Overdue Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Helper Kid");

        // Add a daily BONUS chore (optional)
        var bonusChoreRequest = new
        {
            DisplayName = "Clean Garage",
            Description = "Extra credit",
            Frequency = new { Type = "daily" },
            IsOptional = true
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", bonusChoreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;

        // Assign to kid (even though it's optional)
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - check overdue (should NOT include the bonus chore)
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);
        
        // Kid should have 0 overdue - bonus chores don't count
        kidOverdue?.OverdueCount.Should().Be(0);
        kidOverdue?.Chores.Should().NotContain(c => c.DisplayName == "Clean Garage");
    }

    [Fact]
    public async Task RegularChore_DefaultsToNotOptional()
    {
        // Arrange
        var household = await CreateHousehold("Regular Chore Family");
        var choreRequest = new
        {
            DisplayName = "Do Homework",
            Description = "Required chore"
            // IsOptional not specified - should default to false
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores[0].IsOptional.Should().BeFalse();
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
    private record ChoreDto(Guid ChoreId, string DisplayName, bool IsOptional);
    private record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);
    private record MemberOverdueDto(Guid MemberId, string Nickname, int OverdueCount, List<OverdueChoreDto> Chores);
    private record OverdueChoreDto(Guid ChoreId, string DisplayName, int OverdueDays);

    #endregion
}
