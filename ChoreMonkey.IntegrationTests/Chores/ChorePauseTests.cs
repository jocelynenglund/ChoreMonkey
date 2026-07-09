using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests.Chores;

/// <summary>
/// Pausing a chore over a date range exempts any missed instances within that
/// window: they don't show as overdue and they incur no salary deduction when
/// the pay period is closed.
/// </summary>
[Collection(nameof(ApiCollection))]
public class ChorePauseTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;
    private const int AdminPin = 1234;

    [Fact]
    public async Task PauseChore_WithWrongPin_IsForbidden()
    {
        var household = await CreateHousehold("Pause Auth Family");
        var choreId = await CreateDailyChore(household.HouseholdId, "Make Bed", startDaysAgo: 3);

        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/pause",
            new { pauseStart = DateTime.UtcNow.AddDays(-2), pauseEnd = DateTime.UtcNow, pinCode = 9999 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PauseChore_WithEndBeforeStart_IsBadRequest()
    {
        var household = await CreateHousehold("Pause Range Family");
        var choreId = await CreateDailyChore(household.HouseholdId, "Make Bed", startDaysAgo: 3);

        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/pause",
            new { pauseStart = DateTime.UtcNow, pauseEnd = DateTime.UtcNow.AddDays(-5), pinCode = AdminPin });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PausedDailyChore_IsNotOverdue()
    {
        // Arrange - a daily chore that's been overdue since yesterday
        var household = await CreateHousehold("Vacation Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Away Kid");
        var choreId = await CreateDailyChore(household.HouseholdId, "Feed Fish", startDaysAgo: 3);
        await AssignChore(household.HouseholdId, choreId, kid.MemberId);

        // Sanity: without a pause it IS overdue
        var before = await GetOverdueChores(household.HouseholdId);
        before.MemberOverdue.First(m => m.MemberId == kid.MemberId)
            .Chores.Should().Contain(c => c.DisplayName == "Feed Fish");

        // Act - pause covering yesterday
        await PauseChore(household.HouseholdId, choreId, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);

        // Assert - no longer overdue
        var after = await GetOverdueChores(household.HouseholdId);
        after.MemberOverdue.First(m => m.MemberId == kid.MemberId)
            .Chores.Should().NotContain(c => c.DisplayName == "Feed Fish");
    }

    [Fact]
    public async Task RemovingPause_RestoresOverdue()
    {
        var household = await CreateHousehold("Back From Vacation Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Back Kid");
        var choreId = await CreateDailyChore(household.HouseholdId, "Water Plants", startDaysAgo: 3);
        await AssignChore(household.HouseholdId, choreId, kid.MemberId);

        var pauseId = await PauseChore(household.HouseholdId, choreId, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);

        // Remove the pause
        var removeResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/pause/{pauseId}/remove",
            new { pinCode = AdminPin });
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Overdue again
        var after = await GetOverdueChores(household.HouseholdId);
        after.MemberOverdue.First(m => m.MemberId == kid.MemberId)
            .Chores.Should().Contain(c => c.DisplayName == "Water Plants");
    }

    [Fact]
    public async Task PausedChore_AppearsInChoreListWithWindow()
    {
        var household = await CreateHousehold("Pause Badge Family");
        var choreId = await CreateDailyChore(household.HouseholdId, "Take Out Trash", startDaysAgo: 3);

        var start = DateTime.UtcNow.Date.AddDays(-2);
        var end = DateTime.UtcNow.Date;
        await PauseChore(household.HouseholdId, choreId, start, end, reason: "Summer holiday");

        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        var chore = chores!.Chores.First(c => c.ChoreId == choreId);
        chore.Pauses.Should().ContainSingle();
        chore.Pauses![0].Start.Date.Should().Be(start);
        chore.Pauses[0].End.Date.Should().Be(end);
        chore.Pauses[0].Reason.Should().Be("Summer holiday");
    }

    [Fact]
    public async Task PausedChore_HasNoDeductionAtPeriodClose()
    {
        // Arrange - a daily required chore never completed over the whole period.
        var household = await CreateHousehold("No Penalty Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Excused Kid");
        await SetSalary(household.HouseholdId, kid.MemberId, baseSalary: 1000);

        // Period the close will target: [prevPayday+1 .. today]. Start the chore
        // well before that so every day in the period is a missed instance.
        var choreId = await CreateDailyChore(household.HouseholdId, "Sweep Floor", startDaysAgo: 40, deduction: 10);
        await AssignChore(household.HouseholdId, choreId, kid.MemberId);

        // Pause the chore across a wide window covering the entire pay period.
        await PauseChore(household.HouseholdId, choreId, DateTime.UtcNow.Date.AddDays(-40), DateTime.UtcNow.Date);

        // Act - close the current period (ends today).
        var close = await ClosePeriod(household.HouseholdId, DateTime.UtcNow.Date);

        // Assert - the excused kid has no deductions for the paused chore.
        var payout = close.Payouts.First(p => p.MemberId == kid.MemberId);
        payout.Deductions.Should().Be(0);
        payout.MissedChores.Should().NotContain(m => m.ChoreName == "Sweep Floor");
        payout.NetPay.Should().Be(1000);
    }

    [Fact]
    public async Task UnpausedChore_HasDeductionAtPeriodClose()
    {
        // Control: identical setup WITHOUT a pause produces deductions.
        var household = await CreateHousehold("Penalty Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Slacker Kid");
        await SetSalary(household.HouseholdId, kid.MemberId, baseSalary: 1000);

        var choreId = await CreateDailyChore(household.HouseholdId, "Sweep Floor", startDaysAgo: 40, deduction: 10);
        await AssignChore(household.HouseholdId, choreId, kid.MemberId);

        var close = await ClosePeriod(household.HouseholdId, DateTime.UtcNow.Date);

        var payout = close.Payouts.First(p => p.MemberId == kid.MemberId);
        payout.Deductions.Should().BeGreaterThan(0);
        payout.MissedChores.Should().Contain(m => m.ChoreName == "Sweep Floor");
    }

    [Fact]
    public async Task PausedChore_HasNoDeductionInCurrentPeriodPreview()
    {
        // The live salary preview (GET /salary/current) must also honor pauses.
        var household = await CreateHousehold("Preview Excused Family");
        var invite = await GenerateInvite(household.HouseholdId);
        var kid = await JoinHousehold(household.HouseholdId, invite.InviteId, "Excused Kid");
        await SetSalary(household.HouseholdId, kid.MemberId, baseSalary: 1000);

        var choreId = await CreateDailyChore(household.HouseholdId, "Sweep Floor", startDaysAgo: 40, deduction: 10);
        await AssignChore(household.HouseholdId, choreId, kid.MemberId);

        // Sanity: without a pause the preview shows deductions
        var before = await GetCurrentPeriod(household.HouseholdId);
        before.Members.First(m => m.MemberId == kid.MemberId).Deductions.Should().BeGreaterThan(0);

        // Pause across the whole current period
        await PauseChore(household.HouseholdId, choreId, DateTime.UtcNow.Date.AddDays(-40), DateTime.UtcNow.Date);

        var after = await GetCurrentPeriod(household.HouseholdId);
        var summary = after.Members.First(m => m.MemberId == kid.MemberId);
        summary.Deductions.Should().Be(0);
        summary.MissedChores.Should().NotContain(m => m.ChoreName == "Sweep Floor");
        summary.Projected.Should().Be(1000);
    }

    #region Helpers

    private async Task<CurrentPeriodResponse> GetCurrentPeriod(Guid householdId)
    {
        var response = await _client.GetAsync($"/api/households/{householdId}/salary/current");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrentPeriodResponse>())!;
    }

    private async Task<Guid> CreateDailyChore(Guid householdId, string name, int startDaysAgo, decimal deduction = 10m)
    {
        var request = new
        {
            DisplayName = name,
            Description = name,
            Frequency = new { Type = "daily" },
            StartDate = DateTime.UtcNow.AddDays(-startDaysAgo),
            IsRequired = true,
            MissedDeduction = deduction
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

    private async Task<Guid> PauseChore(Guid householdId, Guid choreId, DateTime start, DateTime end, string? reason = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/chores/{choreId}/pause",
            new { pauseStart = start, pauseEnd = end, pinCode = AdminPin, reason });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PauseChoreResponse>();
        return result!.PauseId;
    }

    private async Task SetSalary(Guid householdId, Guid memberId, decimal baseSalary)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/members/{memberId}/salary",
            new { BaseSalary = baseSalary, DeductionMultiplier = 1.0m, BonusMultiplier = 1.0m });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ClosePeriodResponse> ClosePeriod(Guid householdId, DateTime periodEnd)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/salary/close-period",
            new { PeriodEnd = periodEnd });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClosePeriodResponse>())!;
    }

    private async Task<CreateHouseholdResponse> CreateHousehold(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/households", new { Name = name, PinCode = AdminPin });
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
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{householdId}/join",
            new { InviteId = inviteId, Nickname = nickname });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JoinHouseholdResponse>())!;
    }

    private async Task<GetOverdueResponse> GetOverdueChores(Guid householdId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/households/{householdId}/overdue");
        request.Headers.Add("X-Pin-Code", AdminPin.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GetOverdueResponse>())!;
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GenerateInviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinHouseholdResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record PauseChoreResponse(Guid PauseId);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(Guid ChoreId, string DisplayName, List<PauseWindowDto>? Pauses);
    private record PauseWindowDto(Guid PauseId, DateTime Start, DateTime End, string? Reason);
    private record GetOverdueResponse(List<MemberOverdueDto> MemberOverdue);
    private record MemberOverdueDto(Guid MemberId, string Nickname, int OverdueCount, List<OverdueChoreDto> Chores);
    private record OverdueChoreDto(Guid ChoreId, string DisplayName, string OverduePeriod, DateTime? LastCompleted);
    private record ClosePeriodResponse(Guid PeriodId, DateTime PeriodStart, DateTime PeriodEnd, List<PayoutSummaryDto> Payouts);
    private record PayoutSummaryDto(Guid MemberId, string Name, decimal BaseSalary, decimal Deductions, decimal Bonuses, decimal NetPay, List<MissedChoreDto> MissedChores);
    private record MissedChoreDto(Guid ChoreId, string ChoreName, string Period, decimal Deduction);
    private record CurrentPeriodResponse(DateTime PeriodStart, DateTime PeriodEnd, List<MemberPeriodSummary> Members);
    private record MemberPeriodSummary(Guid MemberId, string Name, decimal BaseSalary, decimal Deductions, decimal Bonuses, decimal Projected, List<MissedChoreDto> MissedChores);

    #endregion
}
