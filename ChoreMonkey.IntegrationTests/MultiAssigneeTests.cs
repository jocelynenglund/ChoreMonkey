namespace ChoreMonkey.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class MultiAssigneeTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AssignChore_ToMultipleMembers_UpdatesAssignment()
    {
        // Arrange
        var household = await CreateHousehold("Multi-Assign Family");
        var invite1 = await GenerateInvite(household.HouseholdId);
        var kid1 = await JoinHousehold(household.HouseholdId, invite1.InviteId, "Alice");
        var invite2 = await GenerateInvite(household.HouseholdId);
        var kid2 = await JoinHousehold(household.HouseholdId, invite2.InviteId, "Bob");
        
        // Add a chore
        var choreRequest = new { DisplayName = "Make Your Bed", Description = "Every morning" };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        // Get the chore ID
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;

        // Act - assign to both kids
        var assignRequest = new { MemberIds = new[] { kid1.MemberId, kid2.MemberId }, AssignToAll = false };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            assignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify assignment persisted
        var updatedChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        updatedChores!.Chores[0].AssignedTo.Should().BeEquivalentTo([kid1.MemberId, kid2.MemberId]);
        updatedChores.Chores[0].AssignedToAll.Should().BeFalse();
    }

    [Fact]
    public async Task AssignChore_ToEveryone_SetsAssignToAllFlag()
    {
        // Arrange
        var household = await CreateHousehold("Everyone Family");
        
        // Add a chore
        var choreRequest = new { DisplayName = "Family Dinner Cleanup", Description = "After dinner" };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        // Get the chore ID
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;

        // Act - assign to everyone
        var assignRequest = new { MemberIds = (Guid[]?)null, AssignToAll = true };
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            assignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var updatedChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        updatedChores!.Chores[0].AssignedToAll.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteChore_MultiAssigned_TracksPerMember()
    {
        // Arrange
        var household = await CreateHousehold("Completion Tracking Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Charlie");
        
        // Add a daily chore
        var choreRequest = new 
        { 
            DisplayName = "Brush Teeth", 
            Description = "Morning routine",
            Frequency = new { Type = "daily" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        // Get the chore ID
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;

        // Assign to both admin and kid
        var assignRequest = new { MemberIds = new[] { household.MemberId, kid.MemberId }, AssignToAll = false };
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            assignRequest);

        // Act - kid completes the chore
        var completeRequest = new { MemberId = kid.MemberId };
        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            completeRequest);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var finalChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var chore = finalChores!.Chores[0];
        
        chore.MemberCompletions.Should().HaveCount(2);
        
        var kidCompletion = chore.MemberCompletions!.First(mc => mc.MemberId == kid.MemberId);
        kidCompletion.CompletedToday.Should().BeTrue();
        kidCompletion.LastCompletedAt.Should().NotBeNull();
        
        var adminCompletion = chore.MemberCompletions!.First(mc => mc.MemberId == household.MemberId);
        adminCompletion.CompletedToday.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteChore_WhenNotAssigned_AutoAssignsThenCompletes()
    {
        // Arrange - chore with no assignment
        var household = await CreateHousehold("Auto-Assign Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Eager Kid");
        
        var choreRequest = new 
        { 
            DisplayName = "Surprise Cleanup", 
            Description = "Kid does it without being asked",
            Frequency = new { Type = "once" }
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;
        
        // Verify no one is assigned initially
        chores.Chores[0].AssignedTo.Should().BeNull();

        // Act - kid completes unassigned chore
        var completeRequest = new { MemberId = kid.MemberId };
        var completeResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/complete",
            completeRequest);

        // Assert - kid should now be assigned AND have completed
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var finalChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        var chore = finalChores!.Chores[0];
        
        // Kid got auto-assigned
        chore.AssignedTo.Should().Contain(kid.MemberId);
        // And completed
        chore.LastCompletedBy.Should().Be(kid.MemberId);
    }

    [Fact]
    public async Task AssignChore_Unassign_ClearsAssignment()
    {
        // Arrange
        var household = await CreateHousehold("Unassign Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Dana");
        
        var choreRequest = new { DisplayName = "Test Chore", Description = "Testing" };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        var choreId = chores!.Chores[0].ChoreId;

        // First assign
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { kid.MemberId }, AssignToAll = false });

        // Act - unassign
        var unassignRequest = new { MemberIds = (Guid[]?)null, AssignToAll = false };
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            unassignRequest);

        // Assert
        var finalChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        finalChores!.Chores[0].AssignedTo.Should().BeNull();
        finalChores.Chores[0].AssignedToAll.Should().BeFalse();
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
    private record ChoreDto(
        Guid ChoreId, 
        string DisplayName, 
        string Description, 
        Guid[]? AssignedTo,
        bool AssignedToAll,
        FrequencyDto? Frequency,
        DateTime? LastCompletedAt,
        Guid? LastCompletedBy,
        List<MemberCompletionDto>? MemberCompletions);
    private record FrequencyDto(string Type, string[]? Days, int? IntervalDays);
    private record MemberCompletionDto(Guid MemberId, bool CompletedToday, DateTime? LastCompletedAt);

    #endregion
}
