using ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberNickname;
using ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberStatus;
using ChoreMonkey.Core.Feature.Members.Commands.JoinHousehold;
using ChoreMonkey.Core.Feature.Members.Commands.RemoveMember;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Members chapter, commands: one test per scenario, the happy case first, then each refusal or branch.
/// Checking the admin PIN belongs to the store adapter, so Remove Member is handed the verdict.
/// </summary>
public class MembersSpecs
{
    static readonly HistoryChapter Members = new("Members");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid InviteId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    static readonly Guid NewMember = Guid.Parse("a0000000-0000-0000-0000-000000000003");

    static readonly HouseholdCreated Created = new(Home, "Home", "admin-hash");
    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.Empty, "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, InviteId, "Bob");
    static readonly InviteGenerated Invite = new(Home, InviteId, $"/join/{Home}/{InviteId}");

    // ── Join Household ───────────────────────────────────────────────────────

    static Decision Join(IReadOnlyList<object> history, JoinHouseholdCommand cmd) => JoinHouseholdSlice.Decide(history, cmd, NewMember);

    [Fact]
    public void A_member_joins_with_an_invite() =>
        Members.Scenario("A member joins with an invite")
            .Given(Invite)
            .When("Join Household", new JoinHouseholdCommand(Home, InviteId, "Cara"), Join)
            .Then(Event<MemberJoinedHousehold>(new { memberId = NewMember, householdId = Home, inviteId = InviteId, nickname = "Cara" }));

    [Fact]
    public void An_invite_can_be_used_more_than_once() =>
        Members.Scenario("An invite can be used more than once")
            .Given(Invite, BobJoined)
            .When("Join Household", new JoinHouseholdCommand(Home, InviteId, "Cara"), Join)
            .Then(Event<MemberJoinedHousehold>(new { memberId = NewMember, inviteId = InviteId, nickname = "Cara" }));

    [Fact]
    public void Joining_needs_an_invite() =>
        Members.Scenario("Joining needs an invite")
            .Given(Created)
            .When("Join Household", new JoinHouseholdCommand(Home, InviteId, "Cara"), Join)
            .ThenNothing("Invalid invite", "Invalid");

    [Fact]
    public void An_unknown_invite_doesnt_work() =>
        Members.Scenario("An invite that was never generated doesn't work")
            .Given(Invite)
            .When("Join Household", new JoinHouseholdCommand(Home, Guid.NewGuid(), "Cara"), Join)
            .ThenNothing("Invalid invite", "Invalid");

    // ── Remove Member ────────────────────────────────────────────────────────

    static Decision Remove(IReadOnlyList<object> history, RemoveMemberCommand cmd) => RemoveMemberSlice.Decide(history, cmd, true);

    static RemoveMemberCommand RemoveBob(Guid? by = null) => new(Home, Bob, by ?? Ann, "1234");

    [Fact]
    public void A_member_is_removed() =>
        Members.Scenario("A member is removed")
            .Given(Created, AnnJoined, BobJoined)
            .When("Remove Member", RemoveBob(), Remove)
            .Then(Event<MemberRemoved>(new { householdId = Home, memberId = Bob, removedByMemberId = Ann, nickname = "Bob" }));

    [Fact]
    public void The_removal_records_the_latest_nickname() =>
        Members.Scenario("The removal records the member's latest nickname")
            .Given(Created, AnnJoined, BobJoined, new MemberNicknameChanged(Bob, Home, "Bob", "Bobby"))
            .When("Remove Member", RemoveBob(), Remove)
            .Then(Event<MemberRemoved>(new { memberId = Bob, nickname = "Bobby" }));

    [Fact]
    public void A_member_cant_be_removed_with_the_wrong_admin_pin() =>
        Members.Scenario("A member can't be removed with the wrong admin PIN")
            .Given(Created, AnnJoined, BobJoined)
            .When("Remove Member", RemoveBob(), (h, c) => RemoveMemberSlice.Decide(h, c, false))
            .ThenNothing("Invalid admin PIN", "Forbidden");

    [Fact]
    public void A_member_of_an_unknown_household_cant_be_removed() =>
        Members.Scenario("A member of an unknown household can't be removed")
            .Given()
            .When("Remove Member", RemoveBob(), Remove)
            .ThenNothing("Household not found", "NotFound");

    [Fact]
    public void An_unknown_member_cant_be_removed() =>
        Members.Scenario("An unknown member can't be removed")
            .Given(Created, AnnJoined)
            .When("Remove Member", RemoveBob(), Remove)
            .ThenNothing("Member not found", "NotFound");

    [Fact]
    public void A_removed_member_cant_be_removed_again() =>
        Members.Scenario("A removed member can't be removed again")
            .Given(Created, AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .When("Remove Member", RemoveBob(), Remove)
            .ThenNothing("Member already removed", "Invalid");

    [Fact]
    public void Nobody_removes_themselves() =>
        Members.Scenario("Nobody removes themselves")
            .Given(Created, AnnJoined, BobJoined)
            .When("Remove Member", RemoveBob(Bob), Remove)
            .ThenNothing("Cannot remove yourself", "Invalid");

    // ── Change Member Nickname ───────────────────────────────────────────────

    static Decision Rename(IReadOnlyList<object> history, ChangeMemberNicknameCommand cmd) => ChangeMemberNicknameSlice.Decide(history, cmd);

    [Fact]
    public void A_nickname_is_changed() =>
        Members.Scenario("A nickname is changed")
            .Given(AnnJoined)
            .When("Change Member Nickname", new ChangeMemberNicknameCommand(Home, Ann, "Annie"), Rename)
            .Then(Event<MemberNicknameChanged>(new { memberId = Ann, householdId = Home, oldNickname = "Ann", newNickname = "Annie" }));

    [Fact]
    public void A_second_change_replaces_the_latest_nickname() =>
        Members.Scenario("A second change records the latest nickname as the old one")
            .Given(
                AnnJoined,
                new MemberNicknameChanged(Ann, Home, "Ann", "Annie") { TimestampUtc = "2026-03-01T10:00:00.0000000Z" },
                new MemberNicknameChanged(Ann, Home, "Annie", "An") { TimestampUtc = "2026-03-02T10:00:00.0000000Z" })
            .When("Change Member Nickname", new ChangeMemberNicknameCommand(Home, Ann, "Ann-Marie"), Rename)
            .Then(Event<MemberNicknameChanged>(new { memberId = Ann, oldNickname = "An", newNickname = "Ann-Marie" }));

    [Fact]
    public void Keeping_the_same_nickname_records_nothing() =>
        Members.Scenario("Keeping the same nickname records nothing")
            .Given(AnnJoined)
            .When("Change Member Nickname", new ChangeMemberNicknameCommand(Home, Ann, "Ann"), Rename)
            .ThenNothing();

    [Fact]
    public void An_unknown_member_cant_change_nickname() =>
        Members.Scenario("An unknown member can't change nickname")
            .Given(AnnJoined)
            .When("Change Member Nickname", new ChangeMemberNicknameCommand(Home, Bob, "Bobby"), Rename)
            .ThenNothing("Member not found", "NotFound");

    // ── Change Member Status ─────────────────────────────────────────────────

    static Decision Status(IReadOnlyList<object> history, ChangeMemberStatusCommand cmd) => ChangeMemberStatusSlice.Decide(history, cmd);

    [Fact]
    public void A_status_is_set() =>
        Members.Scenario("A status is set")
            .Given(AnnJoined)
            .When("Change Member Status", new ChangeMemberStatusCommand(Home, Ann, "On holiday"), Status)
            .Then(Event<MemberStatusChanged>(new { memberId = Ann, householdId = Home, status = "On holiday" }));

    [Fact]
    public void The_same_status_is_recorded_again() =>
        Members.Scenario("The same status is recorded again")
            .Given(AnnJoined, new MemberStatusChanged(Ann, Home, "On holiday"))
            .When("Change Member Status", new ChangeMemberStatusCommand(Home, Ann, "On holiday"), Status)
            .Then(Event<MemberStatusChanged>(new { memberId = Ann, status = "On holiday" }));

    [Fact]
    public void An_unknown_member_cant_change_status() =>
        Members.Scenario("An unknown member can't change status")
            .Given(AnnJoined)
            .When("Change Member Status", new ChangeMemberStatusCommand(Home, Bob, "Away"), Status)
            .ThenNothing("Member not found", "NotFound");
}
