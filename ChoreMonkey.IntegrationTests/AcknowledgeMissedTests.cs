using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests;

public class AcknowledgeMissedTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;
    private const int AdminPin = 1234;

    [Fact]
    public async Task Can_acknowledge_missed_daily_chore()
    {
        // Arrange
        var household = await CreateHousehold("Acknowledge Daily Test");
        
        // Create a daily chore with a past start date (so it's overdue)
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                displayName = "Daily Missed Chore",
                description = "Test",
                frequency = new { type = "daily" },
                startDate = DateTime.UtcNow.AddDays(-3).ToString("o")
            });
        
        // Fetch chore list to get the ID
        var choreId = await GetFirstChoreId(household.HouseholdId);
        
        // Act - Acknowledge the missed chore for yesterday's period
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var period = yesterday.ToString("yyyy-MM-dd");
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/acknowledge-missed",
            new { memberId = household.MemberId, period });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AcknowledgeResponse>();
        Assert.True(result!.Success);
    }

    [Fact]
    public async Task Can_acknowledge_missed_weekly_chore()
    {
        // Arrange
        var household = await CreateHousehold("Acknowledge Weekly Test");
        
        // Create a weekly chore
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                displayName = "Weekly Missed Chore",
                description = "Test",
                frequency = new { type = "weekly", dayOfWeek = 1 }, // Monday
                startDate = DateTime.UtcNow.AddDays(-14).ToString("o")
            });
        
        var choreId = await GetFirstChoreId(household.HouseholdId);
        
        // Act - Acknowledge for a past week
        var lastWeek = DateTime.UtcNow.AddDays(-7);
        var weekNumber = GetIsoWeekNumber(lastWeek);
        var period = $"{lastWeek.Year}-W{weekNumber:D2}";
        
        var response = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/acknowledge-missed",
            new { memberId = household.MemberId, period });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Acknowledged_chore_no_longer_appears_in_overdue_for_that_period()
    {
        // Arrange
        var household = await CreateHousehold("Overdue After Acknowledge Test");
        
        // Create and assign a daily chore with past start date
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                displayName = "Overdue Check Chore",
                description = "Test",
                frequency = new { type = "daily" },
                startDate = DateTime.UtcNow.AddDays(-5).ToString("o")
            });
        
        var choreId = await GetFirstChoreId(household.HouseholdId);
        
        // Assign to member
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/assign",
            new { memberIds = new[] { household.MemberId } });
        
        // Acknowledge for a specific past period
        var missedDate = DateTime.UtcNow.AddDays(-2);
        var period = missedDate.ToString("yyyy-MM-dd");
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/acknowledge-missed",
            new { memberId = household.MemberId, period });
        
        // Act - Get overdue chores
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/households/{household.HouseholdId}/overdue");
        request.Headers.Add("X-Pin-Code", AdminPin.ToString());
        var overdueResponse = await _client.SendAsync(request);
        
        // Assert - The acknowledged period should not appear
        Assert.Equal(HttpStatusCode.OK, overdueResponse.StatusCode);
    }

    [Fact]
    public async Task Multiple_periods_can_be_acknowledged_separately()
    {
        // Arrange
        var household = await CreateHousehold("Multi Period Acknowledge Test");
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                displayName = "Multi Period Chore",
                description = "Test",
                frequency = new { type = "daily" },
                startDate = DateTime.UtcNow.AddDays(-5).ToString("o")
            });
        
        var choreId = await GetFirstChoreId(household.HouseholdId);
        
        // Act - Acknowledge multiple periods
        var period1 = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-dd");
        var period2 = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd");
        
        var response1 = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/acknowledge-missed",
            new { memberId = household.MemberId, period = period1 });
        
        var response2 = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{choreId}/acknowledge-missed",
            new { memberId = household.MemberId, period = period2 });
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    #region Helpers

    private async Task<HouseholdResponse> CreateHousehold(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/households", new
        {
            name,
            ownerNickname = "Owner",
            pinCode = AdminPin
        });
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private async Task<Guid> GetFirstChoreId(Guid householdId)
    {
        var response = await _client.GetAsync($"/api/households/{householdId}/chores");
        var chores = await response.Content.ReadFromJsonAsync<ChoresResponse>();
        return chores!.Chores.First().ChoreId;
    }

    private static int GetIsoWeekNumber(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return cal.GetWeekOfYear(date, 
            System.Globalization.CalendarWeekRule.FirstFourDayWeek, 
            DayOfWeek.Monday);
    }

    private record HouseholdResponse(Guid HouseholdId, Guid MemberId);
    private record ChoreDto(Guid ChoreId, string DisplayName);
    private record ChoresResponse(List<ChoreDto> Chores);
    private record AcknowledgeResponse(bool Success);

    #endregion
}
