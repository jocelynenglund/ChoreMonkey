namespace ChoreMonkey.IntegrationTests.Salary;

[Collection(nameof(ApiCollection))]
public class ChoreRatesTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task SetBonusRate_AppearsInChoreList()
    {
        // Arrange
        var household = await CreateHousehold("Bonus Rate Family");
        var choreRequest = new
        {
            DisplayName = "Wash the Car",
            Description = "Bonus chore",
            IsOptional = true
        };
        var choreResponse = await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        var chore = await choreResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        // Act - set bonus rate
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{chore!.Id}/rates",
            new { DeductionRate = 0, BonusRate = 50 });

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].BonusRate.Should().Be(50);
        chores.Chores[0].DeductionRate.Should().Be(0);
    }

    [Fact]
    public async Task SetDeductionRate_AppearsInChoreList()
    {
        // Arrange
        var household = await CreateHousehold("Deduction Rate Family");
        var choreRequest = new
        {
            DisplayName = "Clean Room",
            Description = "Required chore",
            IsRequired = true,
            MissedDeduction = 10
        };
        var choreResponse = await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        var chore = await choreResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        // Act - set deduction rate
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{chore!.Id}/rates",
            new { DeductionRate = 25, BonusRate = 0 });

        // Assert
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].DeductionRate.Should().Be(25);
        chores.Chores[0].BonusRate.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRates_NewValuesReturned()
    {
        // Arrange
        var household = await CreateHousehold("Update Rates Family");
        var choreRequest = new
        {
            DisplayName = "Mow Lawn",
            Description = "Bonus chore",
            IsOptional = true
        };
        var choreResponse = await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);
        var chore = await choreResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        // Set initial rate
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{chore!.Id}/rates",
            new { DeductionRate = 0, BonusRate = 30 });

        // Act - update to new rate
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{chore.Id}/rates",
            new { DeductionRate = 0, BonusRate = 75 });

        // Assert - should have new rate
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores[0].BonusRate.Should().Be(75);
    }

    [Fact]
    public async Task ChoreWithoutRates_ReturnsNull()
    {
        // Arrange
        var household = await CreateHousehold("No Rates Family");
        var choreRequest = new
        {
            DisplayName = "Random Task",
            Description = "No rates set"
        };
        await _client.PostAsJsonAsync($"/api/households/{household.HouseholdId}/chores", choreRequest);

        // Act
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        // Assert - rates should be null when not set
        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].DeductionRate.Should().BeNull();
        chores.Chores[0].BonusRate.Should().BeNull();
    }

    [Fact]
    public async Task MultipleChores_EachHasOwnRates()
    {
        // Arrange
        var household = await CreateHousehold("Multiple Rates Family");
        
        // Create bonus chore
        var bonusChoreResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Wash Car", IsOptional = true });
        var bonusChore = await bonusChoreResponse.Content.ReadFromJsonAsync<AddChoreResponse>();
        
        // Create required chore
        var requiredChoreResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Do Homework", IsRequired = true });
        var requiredChore = await requiredChoreResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        // Set rates
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{bonusChore!.Id}/rates",
            new { DeductionRate = 0, BonusRate = 100 });
        
        await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{requiredChore!.Id}/rates",
            new { DeductionRate = 15, BonusRate = 0 });

        // Act
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        // Assert
        chores!.Chores.Should().HaveCount(2);
        
        var washCar = chores.Chores.First(c => c.DisplayName == "Wash Car");
        washCar.BonusRate.Should().Be(100);
        washCar.DeductionRate.Should().Be(0);
        
        var homework = chores.Chores.First(c => c.DisplayName == "Do Homework");
        homework.DeductionRate.Should().Be(15);
        homework.BonusRate.Should().Be(0);
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
    private record AddChoreResponse(Guid Id);
    private record GetChoresResponse(List<ChoreDto> Chores);
    private record ChoreDto(
        Guid ChoreId, 
        string DisplayName, 
        bool IsOptional,
        bool IsRequired,
        decimal? DeductionRate,
        decimal? BonusRate);

    #endregion
}
