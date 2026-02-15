namespace ChoreMonkey.IntegrationTests.Chores;

[Collection(nameof(ApiCollection))]
public class OverdueStartDateTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;
    private const string AdminPin = "1234";

    [Fact]
    public async Task DailyChore_CreatedToday_NotOverdue()
    {
        // Arrange - create a household and daily chore TODAY
        var household = await CreateHousehold("Fresh Chore Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "New Kid");

        var choreRequest = new
        {
            DisplayName = "Brand New Chore",
            Description = "Just created",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;

        // Assign to kid
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - check overdue immediately after creation (with admin PIN)
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should NOT be overdue (just created today)
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);

        kidOverdue?.OverdueCount.Should().Be(0);
        kidOverdue?.Chores.Should().NotContain(c => c.DisplayName == "Brand New Chore");
    }

    [Fact]
    public async Task IntervalChore_CreatedToday_NotOverdueUntilIntervalPasses()
    {
        // Arrange - create an interval chore (every 3 days) TODAY
        var household = await CreateHousehold("Interval Start Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Patient Kid");

        var choreRequest = new
        {
            DisplayName = "Water Plants",
            Description = "Every 3 days",
            Frequency = new { Type = "interval", IntervalDays = 3 }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;

        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - check overdue immediately (with admin PIN)
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should NOT be overdue (interval hasn't passed since creation)
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);

        kidOverdue?.OverdueCount.Should().Be(0);
    }

    [Fact]
    public async Task WeeklyChore_CreatedToday_NotOverdueForPastDaysThisWeek()
    {
        // Arrange - if today is Wednesday and we create a Monday chore,
        // it shouldn't be overdue because it was created AFTER Monday
        var household = await CreateHousehold("Weekly Start Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Weekly Kid");

        // Create a chore for a day that already passed this week
        var choreRequest = new
        {
            DisplayName = "Monday Task",
            Description = "Every Monday",
            Frequency = new { Type = "weekly", Days = new[] { "monday" } }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;

        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act (with admin PIN)
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should NOT be overdue if the chore was created after the scheduled day
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);

        // The chore was just created, so any "missed" days before creation don't count
        kidOverdue?.Chores.Should().NotContain(c => c.DisplayName == "Monday Task");
    }

    [Fact]
    public async Task ChoreCreatedYesterday_CompletedYesterday_NotOverdueToday()
    {
        // Arrange
        var household = await CreateHousehold("Caught Up Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Diligent Kid");

        var choreRequest = new
        {
            DisplayName = "Daily Task",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;

        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Complete it "yesterday" retroactively
        var yesterday = DateTime.UtcNow.AddDays(-1);
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId, CompletedAt = yesterday });

        // Act (with admin PIN)
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - completed yesterday, so not overdue today
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);

        kidOverdue?.OverdueCount.Should().Be(0);
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

    private async Task<GetOverdueResponse> GetOverdueChores(Guid householdId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/overdue");
        request.Headers.Add("X-Pin-Code", AdminPin);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GetOverdueResponse>())!;
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);
    private record MemberOverdueDto(Guid MemberId, string Nickname, int OverdueCount, List<OverdueChoreDto> Chores);
    private record OverdueChoreDto(Guid ChoreId, string DisplayName, string OverduePeriod);

    #endregion
}
