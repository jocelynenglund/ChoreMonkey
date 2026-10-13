using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests.Chores;

/// <summary>
/// A member who has done none of their obligatory chores for the past two
/// months gets a "notice of disconnection" on load, until they do one.
/// </summary>
[Collection(nameof(ApiCollection))]
public class DisconnectionNoticeTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;
    private const int AdminPin = 1234;

    [Fact]
    public async Task NothingDoneForTwoMonths_IsDisconnected()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Slacker Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 90);
        var trash = await CreateChore(householdId, "Trash", startDaysAgo: 90, frequency: "weekly");
        await AssignChore(householdId, dishes, kidId);
        await AssignChore(householdId, trash, kidId);

        var notice = await GetNotice(householdId, kidId);

        notice.IsDisconnected.Should().BeTrue();
        notice.MissedChores.Select(c => c.DisplayName).Should().BeEquivalentTo("Dishes", "Trash");
    }

    [Fact]
    public async Task OneObligatoryChoreDoneInWindow_IsNotDisconnected()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Partial Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 90);
        var trash = await CreateChore(householdId, "Trash", startDaysAgo: 90);
        await AssignChore(householdId, dishes, kidId);
        await AssignChore(householdId, trash, kidId);
        await CompleteChore(householdId, trash, kidId, DateTime.UtcNow.AddDays(-20));

        var notice = await GetNotice(householdId, kidId);

        notice.IsDisconnected.Should().BeFalse();
        notice.MissedChores.Should().BeEmpty();
    }

    [Fact]
    public async Task CompletionOlderThanTwoMonths_StillDisconnected()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Long Ago Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 120);
        await AssignChore(householdId, dishes, kidId);
        await CompleteChore(householdId, dishes, kidId, DateTime.UtcNow.AddDays(-75));

        var notice = await GetNotice(householdId, kidId);

        notice.IsDisconnected.Should().BeTrue();
        notice.MissedChores.Should().ContainSingle().Which.LastCompleted.Should().NotBeNull();
    }

    [Fact]
    public async Task CompletingAChore_RestoresConnection()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Reformed Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 90);
        await AssignChore(householdId, dishes, kidId);
        (await GetNotice(householdId, kidId)).IsDisconnected.Should().BeTrue();

        await CompleteChore(householdId, dishes, kidId, DateTime.UtcNow);

        (await GetNotice(householdId, kidId)).IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public async Task ChoresNewerThanTwoMonths_DoNotDisconnect()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("New Chores Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 30);
        await AssignChore(householdId, dishes, kidId);

        (await GetNotice(householdId, kidId)).IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public async Task OptionalChores_DoNotCount()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Bonus Only Family");
        var bonus = await CreateChore(householdId, "Wash Car", startDaysAgo: 90, isOptional: true);
        await AssignChore(householdId, bonus, kidId);

        (await GetNotice(householdId, kidId)).IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public async Task PausedChores_AreExcused()
    {
        var (householdId, kidId) = await CreateHouseholdWithKid("Excused Family");
        var dishes = await CreateChore(householdId, "Dishes", startDaysAgo: 90);
        await AssignChore(householdId, dishes, kidId);

        var pause = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{dishes}/pause",
            new { pauseStart = DateTime.UtcNow.AddDays(-14), pauseEnd = DateTime.UtcNow.AddDays(-7), pinCode = AdminPin });
        pause.EnsureSuccessStatusCode();

        (await GetNotice(householdId, kidId)).IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownMember_IsNotFound()
    {
        var (householdId, _) = await CreateHouseholdWithKid("Stranger Family");

        var response = await _client.GetAsync(
            $"/api/households/{householdId}/disconnection-notice?memberId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region Helpers

    private async Task<DisconnectionNoticeResponse> GetNotice(Guid householdId, Guid memberId)
    {
        var response = await _client.GetAsync(
            $"/api/households/{householdId}/disconnection-notice?memberId={memberId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DisconnectionNoticeResponse>())!;
    }

    private async Task<(Guid HouseholdId, Guid KidId)> CreateHouseholdWithKid(string name)
    {
        var created = await _client.PostAsJsonAsync("/api/households", new { Name = name, PinCode = AdminPin });
        created.EnsureSuccessStatusCode();
        var household = (await created.Content.ReadFromJsonAsync<CreateHouseholdResponse>())!;

        var inviteResponse = await _client.PostAsync($"/api/households/{household.HouseholdId}/invite", null);
        inviteResponse.EnsureSuccessStatusCode();
        var invite = (await inviteResponse.Content.ReadFromJsonAsync<GenerateInviteResponse>())!;

        var joined = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/join",
            new { InviteId = invite.InviteId, Nickname = "Kid" });
        joined.EnsureSuccessStatusCode();
        var kid = (await joined.Content.ReadFromJsonAsync<JoinHouseholdResponse>())!;

        return (household.HouseholdId, kid.MemberId);
    }

    private async Task<Guid> CreateChore(Guid householdId, string name, int startDaysAgo,
        string frequency = "daily", bool isOptional = false)
    {
        var request = new
        {
            DisplayName = name,
            Description = name,
            Frequency = new { Type = frequency },
            StartDate = DateTime.UtcNow.AddDays(-startDaysAgo),
            IsOptional = isOptional,
            IsRequired = !isOptional,
        };
        var response = await _client.PostAsJsonAsync($"/api/households/{householdId}/chores", request);
        response.EnsureSuccessStatusCode();

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>($"/api/households/{householdId}/chores");
        return chores!.Chores.First(c => c.DisplayName == name).ChoreId;
    }

    private async Task AssignChore(Guid householdId, Guid choreId, Guid memberId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/assign",
            new { MemberIds = new[] { memberId }, AssignToAll = false });
        response.EnsureSuccessStatusCode();
    }

    private async Task CompleteChore(Guid householdId, Guid choreId, Guid memberId, DateTime completedAt)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/complete",
            new { MemberId = memberId, CompletedAt = completedAt });
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record DisconnectionNoticeResponse(bool IsDisconnected, DateTime WindowStart, List<MissedChoreDto> MissedChores);
    private record MissedChoreDto(Guid ChoreId, string DisplayName, string? FrequencyType, DateTime? LastCompleted);

    #endregion
}
