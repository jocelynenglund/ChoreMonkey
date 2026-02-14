namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class OverdueChoresTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task DailyChore_NotCompletedYesterday_IsOverdue()
    {
        // Arrange
        var household = await CreateHousehold("Overdue Test Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Lazy Kid");
        
        // Add a daily chore
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
        
        // Assign to kid
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - Get overdue chores (kid hasn't completed anything)
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        result!.MemberOverdue.Should().NotBeEmpty();
        
        var kidOverdue = result.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.OverdueCount.Should().BeGreaterThan(0);
        kidOverdue.Chores.Should().Contain(c => c.DisplayName == "Make Bed");
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
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.OverdueCount.Should().Be(0);
        kidOverdue.Chores.Should().BeEmpty();
    }

    [Fact]
    public async Task IntervalChore_PastInterval_IsOverdue()
    {
        // Arrange
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

        // Complete it 5 days ago (overdue by 2 days)
        var fiveDaysAgo = DateTime.UtcNow.AddDays(-5);
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = kid.MemberId, CompletedAt = fiveDaysAgo });

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.Chores.Should().Contain(c => c.DisplayName == "Water Plants" && c.OverdueDays >= 2);
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
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        var kidOverdue = result!.MemberOverdue.First(m => m.MemberId == kid.MemberId);
        kidOverdue.Chores.Should().NotContain(c => c.DisplayName == "Fix Bike");
    }

    [Fact]
    public async Task MembersWithNoOverdue_ShowZeroCount()
    {
        // Arrange
        var household = await CreateHousehold("Mixed Family");
        var invite1 = await GenerateInvite(household.HouseholdId);
        var goodKid = await JoinHousehold(household.HouseholdId, invite1.InviteId, "Good Kid");
        var invite2 = await GenerateInvite(household.HouseholdId);
        var lazyKid = await JoinHousehold(household.HouseholdId, invite2.InviteId, "Lazy Kid");
        
        var choreRequest = new 
        { 
            DisplayName = "Daily Task", 
            Description = "Daily",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var choreId = chores!.Chores[0].ChoreId;
        
        // Assign to both kids
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { goodKid.MemberId, lazyKid.MemberId }, AssignToAll = false });

        // Good kid completes it
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            new { MemberId = goodKid.MemberId });

        // Act
        var response = await _client.GetAsync($"/api/households/{household.HouseholdId}/overdue");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetOverdueResponse>();
        
        var goodKidOverdue = result!.MemberOverdue.First(m => m.MemberId == goodKid.MemberId);
        goodKidOverdue.OverdueCount.Should().Be(0);
        
        var lazyKidOverdue = result.MemberOverdue.First(m => m.MemberId == lazyKid.MemberId);
        lazyKidOverdue.OverdueCount.Should().Be(1);
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
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);
    private record MemberOverdueDto(Guid MemberId, string Nickname, int OverdueCount, List<OverdueChoreDto> Chores);
    private record OverdueChoreDto(Guid ChoreId, string DisplayName, int OverdueDays, DateTime? LastCompleted);

    #endregion
}
