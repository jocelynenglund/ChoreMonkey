namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class MyChoresTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task DailyChore_NotCompletedToday_ShowsAsPending()
    {
        // Arrange
        var household = await CreateHousehold("Pending Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Lazy Kid");
        
        await CreateAndAssignChore(household.HouseholdId, "Make Bed", "daily", kid.MemberId);

        // Act
        var result = await GetMyChores(household.HouseholdId, kid.MemberId);

        // Assert
        result.Pending.Should().ContainSingle(c => c.DisplayName == "Make Bed");
        result.Overdue.Should().BeEmpty();
        result.Completed.Should().BeEmpty();
    }

    [Fact]
    public async Task DailyChore_CompletedToday_ShowsAsCompleted()
    {
        // Arrange
        var household = await CreateHousehold("Completed Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Good Kid");
        
        var choreId = await CreateAndAssignChore(household.HouseholdId, "Brush Teeth", "daily", kid.MemberId);
        
        // Complete the chore
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId });

        // Act
        var result = await GetMyChores(household.HouseholdId, kid.MemberId);

        // Assert
        result.Completed.Should().ContainSingle(c => c.DisplayName == "Brush Teeth");
        result.Pending.Should().BeEmpty();
        result.Overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task DailyChore_MissedYesterday_ShowsAsOverdue()
    {
        // Arrange
        var household = await CreateHousehold("Overdue Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Forgetful Kid");
        
        // Create chore with start date 2 days ago so yesterday counts
        var choreId = await CreateAndAssignChore(
            household.HouseholdId, 
            "Feed Fish", 
            "daily", 
            kid.MemberId,
            startDate: DateTime.UtcNow.AddDays(-2));

        // Act
        var result = await GetMyChores(household.HouseholdId, kid.MemberId);

        // Assert - should show as overdue (missed yesterday) AND pending (for today)
        result.Overdue.Should().ContainSingle(c => c.DisplayName == "Feed Fish" && c.OverduePeriod == "yesterday");
        result.Pending.Should().ContainSingle(c => c.DisplayName == "Feed Fish"); // Still pending for today
        result.Completed.Should().BeEmpty();
    }

    [Fact]
    public async Task WeeklyChore_NotCompletedThisWeek_ShowsAsPending()
    {
        // Arrange
        var household = await CreateHousehold("Weekly Pending Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Weekly Kid");
        
        await CreateAndAssignChore(household.HouseholdId, "Mow Lawn", "weekly", kid.MemberId);

        // Act
        var result = await GetMyChores(household.HouseholdId, kid.MemberId);

        // Assert
        result.Pending.Should().ContainSingle(c => c.DisplayName == "Mow Lawn");
    }

    [Fact]
    public async Task OptionalChore_NeverShowsAsOverdue()
    {
        // Arrange
        var household = await CreateHousehold("Bonus Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Bonus Kid");
        
        // Create optional chore with old start date
        var choreRequest = new
        {
            DisplayName = "Wash Car",
            Frequency = new { Type = "daily" },
            IsOptional = true,
            StartDate = DateTime.UtcNow.AddDays(-5)
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId } });

        // Act
        var result = await GetMyChores(household.HouseholdId, kid.MemberId);

        // Assert - optional chores show as pending, never overdue
        result.Pending.Should().ContainSingle(c => c.DisplayName == "Wash Car");
        result.Overdue.Should().BeEmpty();
    }

    [Fact]
    public async Task ChoreAssignedToOtherMember_NotInMyChores()
    {
        // Arrange
        var household = await CreateHousehold("Multi-member Family");
        var invite1 = await GenerateInvite(household.HouseholdId);
        var kid1 = await JoinHousehold(household.HouseholdId, invite1.InviteId, "Kid One");
        var invite2 = await GenerateInvite(household.HouseholdId);
        var kid2 = await JoinHousehold(household.HouseholdId, invite2.InviteId, "Kid Two");
        
        // Assign to kid1 only
        await CreateAndAssignChore(household.HouseholdId, "Kid1 Task", "daily", kid1.MemberId);

        // Act - get kid2's chores
        var result = await GetMyChores(household.HouseholdId, kid2.MemberId);

        // Assert - kid2 shouldn't see kid1's chore
        result.Pending.Should().BeEmpty();
        result.Overdue.Should().BeEmpty();
        result.Completed.Should().BeEmpty();
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

    private async Task<Guid> CreateAndAssignChore(Guid householdId, string name, string frequencyType, Guid memberId, DateTime? startDate = null)
    {
        var choreRequest = new
        {
            DisplayName = name,
            Frequency = new { Type = frequencyType },
            StartDate = startDate
        };
        await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", choreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{householdId}/chores");
        var choreId = chores!.Chores.First(c => c.DisplayName == name).ChoreId;

        await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { memberId } });

        return choreId;
    }

    private async Task<GetMyChoresResponse> GetMyChores(Guid householdId, Guid memberId)
    {
        var response = await _client.GetAsync($"/api/households/{householdId}/my-chores?memberId={memberId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GetMyChoresResponse>())!;
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record GetMyChoresResponse(
        List<MyChoreDto> Pending,
        List<MyOverdueChoreDto> Overdue,
        List<MyCompletedChoreDto> Completed);
    private record MyChoreDto(Guid ChoreId, string DisplayName, string? FrequencyType, string? DueDescription);
    private record MyOverdueChoreDto(Guid ChoreId, string DisplayName, string? FrequencyType, string OverduePeriod);
    private record MyCompletedChoreDto(Guid ChoreId, string DisplayName, DateTime CompletedAt);

    #endregion
}
