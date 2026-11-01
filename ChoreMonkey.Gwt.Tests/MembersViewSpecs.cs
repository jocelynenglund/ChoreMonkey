using ChoreMonkey.Core.Feature.Members.Queries.ListMembers;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Members chapter, views: who is in the household, with their latest nickname and status.</summary>
public class MembersViewSpecs
{
    static readonly HistoryChapter Members = new("Members");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.Empty, "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");

    // ── List Members ─────────────────────────────────────────────────────────

    [Fact]
    public void The_list_shows_the_members_who_joined() =>
        Members.Scenario("The list shows the members who joined")
            .Given(AnnJoined, BobJoined)
            .ThenView("List Members", ListMembersView.Project, new
            {
                members = new[]
                {
                    new { memberId = Ann, nickname = "Ann", status = (string?)null },
                    new { memberId = Bob, nickname = "Bob", status = (string?)null },
                },
            });

    [Fact]
    public void The_list_shows_the_latest_nickname_and_status() =>
        Members.Scenario("The list shows the latest nickname and status")
            .Given(
                AnnJoined,
                new MemberNicknameChanged(Ann, Home, "Ann", "Annie"),
                new MemberStatusChanged(Ann, Home, "On holiday"),
                new MemberStatusChanged(Ann, Home, "Back"))
            .ThenView("List Members", ListMembersView.Project, new { members = new[] { new { memberId = Ann, nickname = "Annie", status = "Back" } } });

    [Fact]
    public void The_list_leaves_out_removed_members() =>
        Members.Scenario("The list leaves out removed members")
            .Given(AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("List Members", ListMembersView.Project, new { members = new[] { new { memberId = Ann, nickname = "Ann" } } });

    [Fact]
    public void The_list_is_empty_before_anyone_joins() =>
        Members.Scenario("The list is empty before anyone joins")
            .Given()
            .ThenView("List Members", ListMembersView.Project, new { members = Array.Empty<object>() });

    // ── Member Lookup ────────────────────────────────────────────────────────

    [Fact]
    public void The_lookup_finds_members_by_id() =>
        Members.Scenario("The lookup finds members by id")
            .Given(AnnJoined, BobJoined, new MemberNicknameChanged(Bob, Home, "Bob", "Bobby"), new MemberStatusChanged(Bob, Home, "Away"))
            .ThenView("Member Lookup", MemberLookupView.Project, new
            {
                members = new Dictionary<string, object>
                {
                    [Ann.ToString()] = new { id = Ann, nickname = "Ann", status = (string?)null },
                    [Bob.ToString()] = new { id = Bob, nickname = "Bobby", status = "Away" },
                },
            });

    [Fact]
    public void A_removed_member_is_gone_from_the_lookup() =>
        Members.Scenario("A removed member is gone from the lookup")
            .Given(AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("Member Lookup", MemberLookupView.Project, new
            {
                members = new Dictionary<string, object> { [Ann.ToString()] = new { id = Ann, nickname = "Ann" } },
            });

    [Fact]
    public void A_change_for_someone_who_never_joined_is_ignored() =>
        Members.Scenario("A change for someone who never joined is ignored")
            .Given(AnnJoined, new MemberNicknameChanged(Bob, Home, "Bob", "Bobby"), new MemberStatusChanged(Bob, Home, "Away"))
            .ThenView("Member Lookup", MemberLookupView.Project, new
            {
                members = new Dictionary<string, object> { [Ann.ToString()] = new { id = Ann, nickname = "Ann" } },
            });
}
