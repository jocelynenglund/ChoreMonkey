namespace ChoreMonkey.IntegrationTests.Chores;

[Collection(nameof(ApiCollection))]
public class ChoreFrequencyTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AddChore_WithDailyFrequency_StoresFrequency()
    {
        // Arrange
        var household = await CreateHousehold("Daily Chore Family");
        var choreRequest = new 
        { 
            DisplayName = "Make Bed", 
            Description = "Every morning",
            Frequency = new { Type = "daily" }
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        
        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].Frequency.Should().NotBeNull();
        chores.Chores[0].Frequency!.Type.Should().Be("daily");
    }

    [Fact]
    public async Task AddChore_WithWeeklyFrequency_StoresDays()
    {
        // Arrange
        var household = await CreateHousehold("Weekly Chore Family");
        var choreRequest = new 
        { 
            DisplayName = "Take Out Trash", 
            Description = "Trash days",
            Frequency = new { Type = "weekly", Days = new[] { "monday", "thursday" } }
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        
        chores!.Chores[0].Frequency!.Type.Should().Be("weekly");
        chores.Chores[0].Frequency.Days.Should().BeEquivalentTo(new[] { "monday", "thursday" });
    }

    [Fact]
    public async Task AddChore_WithIntervalFrequency_StoresInterval()
    {
        // Arrange
        var household = await CreateHousehold("Interval Chore Family");
        var choreRequest = new 
        { 
            DisplayName = "Water Plants", 
            Description = "Every 3 days",
            Frequency = new { Type = "interval", IntervalDays = 3 }
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        
        chores!.Chores[0].Frequency!.Type.Should().Be("interval");
        chores.Chores[0].Frequency.IntervalDays.Should().Be(3);
    }

    [Fact]
    public async Task AddChore_WithoutFrequency_DefaultsToOneTime()
    {
        // Arrange
        var household = await CreateHousehold("One-time Chore Family");
        var choreRequest = new 
        { 
            DisplayName = "Fix Fence", 
            Description = "One time task"
            // No frequency specified
        };

        // Act
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");
        
        chores!.Chores[0].Frequency.Should().NotBeNull();
        chores.Chores[0].Frequency!.Type.Should().Be("once");
    }

    #region Helpers

    private async Task<CreateHouseholdResponse> CreateHousehold(string name)
    {
        var request = new { Name = name, PinCode = 1234 };
        var response = await _client.PostAsJsonAsync("/api/households", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>())!;
    }

    #endregion

    #region Response Records

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(
        Guid ChoreId, 
        string DisplayName, 
        string Description, 
        Guid? AssignedTo,
        FrequencyDto? Frequency);
    private record FrequencyDto(string Type, string[]? Days, int? IntervalDays);

    #endregion
}
