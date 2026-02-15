namespace ChoreMonkey.IntegrationTests.Chores;

[Collection(nameof(ApiCollection))]
public class OverdueChoresTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;
    private const string AdminPin = "1234";
    private const string MemberPin = "5678";

    [Fact]
    public async Task OverdueEndpoint_RequiresAdminPin()
    {
        // Arrange
        var household = await CreateHousehold("Admin Test Family");

        // Act - no PIN header
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OverdueEndpoint_RejectsMemberPin()
    {
        // Arrange
        var household = await CreateHouseholdWithMemberPin("Member Test Family");

        // Act - use member PIN instead of admin PIN
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{household.HouseholdId}/overdue");
        request.Headers.Add("X-Pin-Code", MemberPin);
        var response = await _client.SendAsync(request);

        // Assert - member PIN should be rejected (admin only)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DailyChore_CreatedTodayAndNeverCompleted_NotOverdueYet()
    {
        // Arrange - a chore created TODAY should NOT be overdue immediately
        var household = await CreateHousehold("Overdue Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "New Kid");
        
        var choreRequest = new 
        { 
            DisplayName = "Make Bed", 
            Description = "Every morning",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - Get overdue chores (with admin PIN)
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should NOT be overdue (just created today)
        var kidOverdue = result!.MemberOverdue.FirstOrDefault(m => m.MemberId == kid.MemberId);
        kidOverdue?.OverdueCount.Should().Be(0);
    }

    [Fact]
    public async Task DailyChore_CompletedToday_NotOverdue()
    {
        // Arrange
        var household = await CreateHousehold("Caught Up Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Good Kid");
        
        var choreRequest = new 
        { 
            DisplayName = "Brush Teeth", 
            Description = "Morning routine",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Complete the chore today
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId });

        // Act
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.OverdueCount.Should().Be(0);
        kidOverdue.Chores.Should().BeEmpty();
    }

    [Fact]
    public async Task IntervalChore_WithinGracePeriod_IsOverdue()
    {
        // Arrange - 3 day interval, completed 4 days ago = 1 day overdue (within grace)
        var household = await CreateHousehold("Interval Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Plant Forgetter");
        
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

        // Complete it 4 days ago (1 day overdue - within 3-day grace period)
        var fourDaysAgo = DateTime.UtcNow.AddDays(-4);
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId, CompletedAt = fourDaysAgo });

        // Act
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should be overdue (within grace period)
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.Chores.Should().Contain(c => c.DisplayName == "Water Plants");
    }

    [Fact]
    public async Task IntervalChore_PastGracePeriod_NotShown()
    {
        // Arrange - 3 day interval, completed 7 days ago = 4 days overdue (past 3-day grace)
        var household = await CreateHousehold("Old Interval Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Plant Forgetter");
        
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

        // Complete it 7 days ago (4 days overdue - past grace period)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId, CompletedAt = sevenDaysAgo });

        // Act
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert - should NOT be shown (past grace period = forgiven)
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.Chores.Should().NotContain(c => c.DisplayName == "Water Plants");
    }

    [Fact]
    public async Task OnceChore_NeverOverdue()
    {
        // Arrange
        var household = await CreateHousehold("One Time Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Procrastinator");
        
        var choreRequest = new 
        { 
            DisplayName = "Fix Bike", 
            Description = "One time task",
            Frequency = new { Type = "once" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - Never completed, but "once" chores can't be overdue
        var result = await GetOverdueChores(household.HouseholdId);

        // Assert
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.Chores.Should().NotContain(c => c.DisplayName == "Fix Bike");
    }

    #region Helpers

    private async Task<CreateHouseholdResponse> CreateHousehold(string name)
    {
        var request = new { Name = name, PinCode = AdminPin };
        var response = await _client.PostAsJsonAsync("/api/households", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>())!;
    }

    private async Task<CreateHouseholdResponse> CreateHouseholdWithMemberPin(string name)
    {
        var request = new { Name = name, PinCode = AdminPin, MemberPinCode = MemberPin };
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
    private record OverdueChoreDto(Guid ChoreId, string DisplayName, string OverduePeriod, DateTime? LastCompleted);

    #endregion
}
