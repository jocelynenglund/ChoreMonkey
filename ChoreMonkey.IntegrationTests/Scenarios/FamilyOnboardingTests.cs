namespace ChoreMonkey.IntegrationTests.Scenarios;

/// <summary>
/// Full end-to-end scenario: Family sets up household, invites kids, assigns chores.
/// </summary>
[Collection(nameof(ApiCollection))]
public class FamilyOnboardingTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task CompleteOnboardingFlow()
    {
        // === Step 1: Parent creates a household ===
        var createRequest = new { Name = "The Johnson Family", PinCode = 4321, OwnerNickname = "Dad" };
        var createResponse = await _client.PostAsJsonAsync("/api/households", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var household = await createResponse.Content.ReadFromJsonAsync<CreateHouseholdResponse>();
        household!.HouseholdId.Should().NotBeEmpty();
        household.Name.Should().Be("The Johnson Family");

        // === Step 2: Parent generates an invite link ===
        var inviteResponse = await _client.PostAsync($"/api/households/{household.HouseholdId}/invite", null);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var invite = await inviteResponse.Content.ReadFromJsonAsync<GenerateInviteResponse>();
        invite!.Link.Should().NotBeNullOrEmpty();

        // === Step 3: First kid joins with the invite ===
        var join1Request = new { InviteId = invite.InviteId, Nickname = "Emma" };
        var join1Response = await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/join", join1Request);
        join1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var emma = await join1Response.Content.ReadFromJsonAsync<JoinHouseholdResponse>();
        emma!.Nickname.Should().Be("Emma");

        // === Step 4: Generate another invite for second kid ===
        var invite2Response = await _client.PostAsync($"/api/households/{household.HouseholdId}/invite", null);
        var invite2 = await invite2Response.Content.ReadFromJsonAsync<GenerateInviteResponse>();

        // === Step 5: Second kid joins ===
        var join2Request = new { InviteId = invite2!.InviteId, Nickname = "Jake" };
        var join2Response = await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/join", join2Request);
        join2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jake = await join2Response.Content.ReadFromJsonAsync<JoinHouseholdResponse>();

        // === Step 6: Verify all members are listed ===
        var membersResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/members");
        var members = await membersResponse.Content.ReadFromJsonAsync<ListMembersResponse>();
        
        members!.Members.Should().HaveCount(3);
        members.Members.Select(m => m.Nickname).Should().BeEquivalentTo(["Dad", "Emma", "Jake"]);

        // === Step 7: Parent adds chores ===
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Clean Room", Description = "Make bed, pick up toys, vacuum" });
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Do Dishes", Description = "Load/unload dishwasher" });
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Take Out Trash", Description = "Empty all bins, take to curb" });

        // === Step 8: Get chores and assign them ===
        var choresResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/chores");
        var chores = await choresResponse.Content.ReadFromJsonAsync<GetChoresResponse>();
        chores!.Chores.Should().HaveCount(3);

        var cleanRoom = chores.Chores.First(c => c.DisplayName == "Clean Room");
        var doDishes = chores.Chores.First(c => c.DisplayName == "Do Dishes");
        var takeTrash = chores.Chores.First(c => c.DisplayName == "Take Out Trash");

        // Assign chores to kids
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{cleanRoom.ChoreId}/assign",
            new { MemberId = emma!.MemberId });
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{doDishes.ChoreId}/assign",
            new { MemberId = jake!.MemberId });
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{takeTrash.ChoreId}/assign",
            new { MemberId = emma.MemberId });

        // === Step 9: Verify final state ===
        var finalChores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        // Emma has 2 chores
        finalChores!.Chores.Where(c => c.AssignedTo == emma.MemberId).Should().HaveCount(2);
        
        // Jake has 1 chore
        finalChores.Chores.Where(c => c.AssignedTo == jake.MemberId).Should().HaveCount(1);
        
        // All chores are assigned
        finalChores.Chores.Should().OnlyContain(c => c.AssignedTo != null);
    }

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record ListMembersResponse(List<MemberDto> Members);
    private record MemberDto(Guid MemberId, string Nickname);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName, string Description, Guid? AssignedTo);

    #endregion
}
