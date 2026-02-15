using System.Net;
using System.Net.Http.Json;

namespace ChoreMonkey.IntegrationTests.Members;

public class RemoveMemberTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fixture.Client;
    private const int AdminPin = 1234;
    private const int MemberPin = 5678;

    [Fact]
    public async Task Admin_can_remove_member()
    {
        // Arrange - Create household with creator
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Remove Test Family",
            ownerNickname = "Admin",
            pinCode = AdminPin,
            memberPinCode = MemberPin
        });
        var household = await createResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        var adminMemberId = household!.MemberId;
        
        // Add another member
        var inviteResponse = await _client.PostAsync(
            $"/api/households/{household.HouseholdId}/invite",
            null);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<InviteResponse>();
        
        var joinResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/join",
            new { inviteId = invite!.InviteId, nickname = "ToBeRemoved" });
        var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>();
        var memberToRemoveId = joinResult!.MemberId;
        
        // Act - Remove the member (as admin)
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/households/{household.HouseholdId}/members/{memberToRemoveId}/remove")
        {
            Content = JsonContent.Create(new { removedByMemberId = adminMemberId })
        };
        request.Headers.Add("X-Pin-Code", AdminPin.ToString());
        var removeResponse = await _client.SendAsync(request);
        
        // Assert - Should succeed
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        
        // Verify member is no longer in the list
        var membersResponse = await _client.GetAsync($"/api/households/{household.HouseholdId}/members");
        var members = await membersResponse.Content.ReadFromJsonAsync<MembersResponse>();
        Assert.DoesNotContain(members!.Members, m => m.MemberId == memberToRemoveId);
        Assert.Contains(members.Members, m => m.MemberId == adminMemberId);
    }

    [Fact]
    public async Task Cannot_remove_member_without_admin_pin()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Pin Test Family",
            ownerNickname = "Admin",
            pinCode = AdminPin,
            memberPinCode = MemberPin
        });
        var household = await createResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        var adminMemberId = household!.MemberId;
        
        // Add another member
        var inviteResponse = await _client.PostAsync($"/api/households/{household.HouseholdId}/invite", null);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<InviteResponse>();
        var joinResponse = await _client.PostAsJsonAsync(
            $"/api/households/{household.HouseholdId}/join",
            new { inviteId = invite!.InviteId, nickname = "Member" });
        var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>();
        
        // Act - Try to remove with wrong PIN
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/households/{household.HouseholdId}/members/{joinResult!.MemberId}/remove")
        {
            Content = JsonContent.Create(new { removedByMemberId = adminMemberId })
        };
        request.Headers.Add("X-Pin-Code", "9999"); // Wrong PIN
        var removeResponse = await _client.SendAsync(request);
        
        // Assert - Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, removeResponse.StatusCode);
    }

    [Fact]
    public async Task Cannot_remove_yourself()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/households", new
        {
            name = "Self Remove Test",
            ownerNickname = "Admin",
            pinCode = AdminPin
        });
        var household = await createResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        var adminMemberId = household!.MemberId;
        
        // Act - Try to remove yourself
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/households/{household.HouseholdId}/members/{adminMemberId}/remove")
        {
            Content = JsonContent.Create(new { removedByMemberId = adminMemberId })
        };
        request.Headers.Add("X-Pin-Code", AdminPin.ToString());
        var removeResponse = await _client.SendAsync(request);
        
        // Assert - Should fail
        Assert.Equal(HttpStatusCode.BadRequest, removeResponse.StatusCode);
    }

    private record HouseholdResponse(Guid HouseholdId, Guid MemberId);
    private record InviteResponse(Guid HouseholdId, Guid InviteId, string Link);
    private record JoinResponse(Guid MemberId, Guid HouseholdId, string Nickname);
    private record MemberDto(Guid MemberId, string Nickname);
    private record MembersResponse(List<MemberDto> Members);
}
