namespace ChoreMonkey.IntegrationTests.Household;

[Collection(nameof(ApiCollection))]
public class HouseholdTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

    [Fact]
    public async Task CreateHousehold_ReturnsHouseholdIdAndMemberId()
    {
        // Arrange
        var request = new { Name = "The Smiths", PinCode = 1234 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/households", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>();
        result.Should().NotBeNull();
        result!.HouseholdId.Should().NotBeEmpty();
        result.MemberId.Should().NotBeEmpty();
        result.Name.Should().Be("The Smiths");
    }

    [Fact]
    public async Task CreateHousehold_WithCustomOwnerNickname_ReturnsCorrectName()
    {
        // Arrange
        var request = new 
        { 
            Name = "Family Home", 
            PinCode = 5678,
            OwnerNickname = "Mom"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/households", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreateHouseholdResponse>();
        result!.Name.Should().Be("Family Home");
    }

    [Fact]
    public async Task GetHousehold_AfterCreate_ReturnsName()
    {
        // Arrange
        var createRequest = new { Name = "Test Household", PinCode = 1111 };
        var createResponse = await _client.PostAsJsonAsync("/api/households", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateHouseholdResponse>();

        // Act
        var response = await _client.GetAsync($"/api/households/{created!.HouseholdId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HouseholdNameResponse>();
        result!.HouseholdName.Should().Be("Test Household");
        result.HouseholdId.Should().Be(created.HouseholdId);
    }

    private record CreateHouseholdResponse(Guid HouseholdId, Guid MemberId, string Name);
    private record HouseholdNameResponse(Guid HouseholdId, string HouseholdName);
}
