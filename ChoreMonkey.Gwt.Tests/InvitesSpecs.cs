using ChoreMonkey.Core.Feature.Invites.Commands.GenerateInvite;
using ChoreMonkey.Core.Feature.Invites.Queries.InviteLink;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Invites chapter: generating an invite link, and the view that shows the latest one.</summary>
public class InvitesSpecs
{
    static readonly HistoryChapter Invites = new("Invites");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid FirstInvite = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    static readonly Guid SecondInvite = Guid.Parse("e0000000-0000-0000-0000-000000000002");

    // ── Generate Invite ──────────────────────────────────────────────────────

    static Decision Generate(IReadOnlyList<object> history, GenerateInviteCommand cmd) => GenerateInviteSlice.Decide(history, cmd, SecondInvite);

    [Fact]
    public void An_invite_is_generated() =>
        Invites.Scenario("An invite is generated")
            .Given()
            .When("Generate Invite", new GenerateInviteCommand(Home), Generate)
            .Then(Event<InviteGenerated>(new { householdId = Home, inviteId = SecondInvite, link = $"/join/{Home}/{SecondInvite}" }));

    [Fact]
    public void A_new_invite_doesnt_cancel_the_old_one() =>
        Invites.Scenario("A new invite doesn't cancel the old one")
            .Given(new HouseholdCreated(Home, "Home", "admin-hash"), new InviteGenerated(Home, FirstInvite, $"/join/{Home}/{FirstInvite}"))
            .When("Generate Invite", new GenerateInviteCommand(Home), Generate)
            .Then(Event<InviteGenerated>(new { inviteId = SecondInvite, link = $"/join/{Home}/{SecondInvite}" }));

    // ── Invite Link ──────────────────────────────────────────────────────────

    [Fact]
    public void The_link_shows_the_latest_invite() =>
        Invites.Scenario("The link shows the latest invite")
            .Given(new InviteGenerated(Home, FirstInvite, $"/join/{Home}/{FirstInvite}"), new InviteGenerated(Home, SecondInvite, $"/join/{Home}/{SecondInvite}"))
            .ThenView("Invite Link", InviteLinkView.Project, new { householdId = Home, inviteId = SecondInvite, link = $"/join/{Home}/{SecondInvite}" });

    [Fact]
    public void There_is_no_link_before_an_invite_is_generated() =>
        Invites.Scenario("There is no link before an invite is generated")
            .Given(new HouseholdCreated(Home, "Home", "admin-hash"))
            .ThenView("Invite Link", InviteLinkView.Project, null);
}
