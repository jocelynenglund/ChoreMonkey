namespace ChoreMonkey.IntegrationTests.Salary;

/// <summary>
/// Covers the call sequence the frontend actually makes when adding a bonus
/// chore with an amount: store.ts generates the chore id, POSTs the chore with
/// it, then AddChoreDialog sets the rates against that same id.
///
/// The existing ChoreRatesTests read the id back out of the AddChore response,
/// so they never exercised the case where the client picks the id.
/// </summary>
[Collection(nameof(ApiCollection))]
public class BonusRateFrontendFlowTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task AddChore_HonoursClientSuppliedChoreId()
    {
        var household = await CreateHousehold("Client Id Family");

        // store.ts: const choreId = crypto.randomUUID()
        var clientChoreId = Guid.NewGuid();

        var addResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                ChoreId = clientChoreId,
                DisplayName = "Wash the Car",
                Description = "Bonus chore",
                IsOptional = true,
            });
        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        added!.Id.Should().Be(
            clientChoreId,
            "the frontend sets rates against the id it generated, so the server must use it");
    }

    [Fact]
    public async Task BonusRate_SetAgainstClientChoreId_ShowsUpInChoreList()
    {
        var household = await CreateHousehold("Bonus Flow Family");

        // 1. store.ts generates the id and POSTs the chore with it.
        var clientChoreId = Guid.NewGuid();
        var addResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new
            {
                ChoreId = clientChoreId,
                DisplayName = "Wash the Car",
                Description = "Bonus chore",
                IsOptional = true,
            });
        addResponse.EnsureSuccessStatusCode();

        // 2. store.ts returns { id: clientChoreId }; AddChoreDialog then calls
        //    onSetRates(result.id, 0, bonus).
        var ratesResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores/{clientChoreId}/rates",
            new { DeductionRate = 0, BonusRate = 50 });

        ratesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. The list reload the user sees.
        var chores = await _client.GetFromJsonAsync<GetChoresResponse>(
            $"/api/households/{household.HouseholdId}/chores");

        chores!.Chores.Should().HaveCount(1);
        chores.Chores[0].BonusRate.Should().Be(50, "the user typed 50 into 'Bonus Earned (kr)'");
    }

    [Fact]
    public async Task AddChore_WithoutChoreId_StillGeneratesOne()
    {
        var household = await CreateHousehold("Server Id Family");

        var addResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/chores",
            new { DisplayName = "Take Out Bins", Description = "No client id" });
        addResponse.EnsureSuccessStatusCode();

        var added = await addResponse.Content.ReadFromJsonAsync<AddChoreResponse>();

        added!.Id.Should().NotBeEmpty("callers that omit ChoreId must still get a usable id");
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
