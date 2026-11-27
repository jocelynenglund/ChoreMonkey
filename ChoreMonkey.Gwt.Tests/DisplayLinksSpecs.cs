using ChoreMonkey.Core.Feature.DisplayLinks.Commands.MakeDisplayLink;
using ChoreMonkey.Core.Feature.DisplayLinks.Commands.RevokeDisplayLink;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLately;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLinks;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayToday;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Display Links chapter: a household admin shares a read-only picture of the household (today's chores and the
/// last 40 days) with a connected screen such as barrio. The link's secret is never an event; only that a link
/// was made or revoked is. "Now" for the views is Wednesday 2026-03-18, 11:00 in Stockholm.
/// </summary>
public class DisplayLinksSpecs
{
    static readonly HistoryChapter Display = new("Display Links");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Link1 = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    static readonly Guid Link2 = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid BinsId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 11, 0, 0);
    const string BaseUrl = "https://choremonkey.example";

    static readonly HouseholdCreated Created = new(Home, "The Smiths", "admin-hash");
    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Bins = new(BinsId, Home, "Bins", "Take out the bins", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static MemberJoinedHousehold Joined(Guid id, string nickname) => new(id, Home, Guid.NewGuid(), nickname);
    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);

    // ── Make Display Link ────────────────────────────────────────────────────

    static Decision Make(IReadOnlyList<object> h, MakeDisplayLinkCommand c) => MakeDisplayLinkSlice.Decide(h, c, true, Link2);

    [Fact]
    public void An_admin_makes_a_display_link() =>
        Display.Scenario("An admin makes a display link")
            .Given(Created)
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "barrio"), Make)
            .Then(Event<DisplayLinkCreated>(new { householdId = Home, linkId = Link2, label = "barrio" }));

    [Fact]
    public void A_link_without_a_label_is_called_barrio() =>
        Display.Scenario("A link without a label is called barrio")
            .Given(Created)
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "  "), Make)
            .Then(Event<DisplayLinkCreated>(new { label = "barrio" }));

    [Fact]
    public void A_display_link_needs_the_admin() =>
        Display.Scenario("A display link needs the admin")
            .Given(Created)
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "barrio"), (h, c) => MakeDisplayLinkSlice.Decide(h, c, false, Link2))
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void A_display_link_for_an_unknown_household_is_refused() =>
        Display.Scenario("A display link for an unknown household is refused")
            .Given()
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "barrio"), Make)
            .ThenNothing("Household not found", "NotFound");

    [Fact]
    public void A_label_cant_be_long() =>
        Display.Scenario("A label can't be long")
            .Given(Created)
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, new string('x', 41)), Make)
            .ThenNothing(MakeDisplayLinkSlice.LabelTooLong, "Invalid");

    [Fact]
    public void A_household_has_at_most_five_links() =>
        Display.Scenario("A household has at most five links")
            .Given(Created,
                new DisplayLinkCreated(Home, Guid.NewGuid(), "a"), new DisplayLinkCreated(Home, Guid.NewGuid(), "b"),
                new DisplayLinkCreated(Home, Guid.NewGuid(), "c"), new DisplayLinkCreated(Home, Guid.NewGuid(), "d"),
                new DisplayLinkCreated(Home, Guid.NewGuid(), "e"))
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "barrio"), Make)
            .ThenNothing(MakeDisplayLinkSlice.TooMany, "Conflict");

    [Fact]
    public void A_revoked_link_makes_room_for_another() =>
        Display.Scenario("A revoked link makes room for another")
            .Given(Created,
                new DisplayLinkCreated(Home, Link1, "a"), new DisplayLinkCreated(Home, Guid.NewGuid(), "b"),
                new DisplayLinkCreated(Home, Guid.NewGuid(), "c"), new DisplayLinkCreated(Home, Guid.NewGuid(), "d"),
                new DisplayLinkCreated(Home, Guid.NewGuid(), "e"), new DisplayLinkRevoked(Home, Link1))
            .When("Make Display Link", new MakeDisplayLinkCommand(Home, "barrio"), Make)
            .Then(Event<DisplayLinkCreated>(new { linkId = Link2 }));

    // ── Revoke Display Link ──────────────────────────────────────────────────

    static Decision Revoke(IReadOnlyList<object> h, RevokeDisplayLinkCommand c) => RevokeDisplayLinkSlice.Decide(h, c, true);

    [Fact]
    public void An_admin_revokes_a_display_link() =>
        Display.Scenario("An admin revokes a display link")
            .Given(Created, new DisplayLinkCreated(Home, Link1, "barrio"))
            .When("Revoke Display Link", new RevokeDisplayLinkCommand(Home, Link1), Revoke)
            .Then(Event<DisplayLinkRevoked>(new { householdId = Home, linkId = Link1 }));

    [Fact]
    public void Revoking_needs_the_admin() =>
        Display.Scenario("Revoking needs the admin")
            .Given(Created, new DisplayLinkCreated(Home, Link1, "barrio"))
            .When("Revoke Display Link", new RevokeDisplayLinkCommand(Home, Link1), (h, c) => RevokeDisplayLinkSlice.Decide(h, c, false))
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void A_link_that_was_never_made_cant_be_revoked() =>
        Display.Scenario("A link that was never made can't be revoked")
            .Given(Created)
            .When("Revoke Display Link", new RevokeDisplayLinkCommand(Home, Link1), Revoke)
            .ThenNothing("Display link not found", "NotFound");

    [Fact]
    public void A_link_is_revoked_once() =>
        Display.Scenario("A link is revoked once")
            .Given(Created, new DisplayLinkCreated(Home, Link1, "barrio"), new DisplayLinkRevoked(Home, Link1))
            .When("Revoke Display Link", new RevokeDisplayLinkCommand(Home, Link1), Revoke)
            .ThenNothing("Display link not found", "NotFound");

    // ── Display Links (the admin's list) ─────────────────────────────────────

    [Fact]
    public void No_links_before_one_is_made() =>
        Display.Scenario("No links before one is made")
            .Given(Created)
            .ThenView("Display Links", DisplayLinksView.Project, new { links = Array.Empty<object>() });

    [Fact]
    public void Live_links_are_listed_and_revoked_ones_are_not() =>
        Display.Scenario("Live links are listed and revoked ones are not")
            .Given(Created, new DisplayLinkCreated(Home, Link1, "kitchen"), new DisplayLinkCreated(Home, Link2, "barrio"), new DisplayLinkRevoked(Home, Link1))
            .ThenView("Display Links", DisplayLinksView.Project, new { links = new object[] { new { linkId = Link2, label = "barrio" } } });

    // ── Display Today ────────────────────────────────────────────────────────

    static DisplayTodayResponse? Today(IReadOnlyList<object> h) => DisplayTodayView.Project(h, Now, BaseUrl);

    [Fact]
    public void Nothing_is_on_the_list_for_a_new_household() =>
        Display.Scenario("Nothing is on the list for a new household")
            .Given(Created)
            .ThenView("Display Today", Today, new { householdName = "The Smiths", day = "2026-03-18", householdUrl = $"{BaseUrl}/household/{Home}", chores = Array.Empty<object>() });

    [Fact]
    public void A_vanity_name_is_the_household_url() =>
        Display.Scenario("A vanity name is the household URL")
            .Given(Created, new HouseholdSlugSet(Home, "the-smiths", Now))
            .ThenView("Display Today", Today, new { householdUrl = $"{BaseUrl}/h/the-smiths" });

    [Fact]
    public void An_assigned_chore_is_open() =>
        Display.Scenario("An assigned chore is open")
            .Given(Created, Joined(Ann, "Ann"), Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Display Today", Today, new { chores = new object[] { new { choreId = DishesId, title = "Dishes", state = "open" } } });

    [Fact]
    public void A_chore_done_today_is_done_and_says_by_whom() =>
        Display.Scenario("A chore done today is done and says by whom")
            .Given(Created, Joined(Ann, "Ann"), Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, new DateTime(2026, 3, 18, 9, 0, 0)))
            .ThenView("Display Today", Today, new { chores = new object[] { new { choreId = DishesId, title = "Dishes", state = "done", by = "Ann" } } });

    [Fact]
    public void A_daily_chore_done_yesterday_is_open_again() =>
        Display.Scenario("A daily chore done yesterday is open again")
            .Given(Created, Joined(Ann, "Ann"), Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, new DateTime(2026, 3, 17, 9, 0, 0)))
            .ThenView("Display Today", Today, new { chores = new object[] { new { title = "Dishes", state = "open" } } });

    [Fact]
    public void A_shared_chore_is_listed_once_and_done_when_anyone_did_it() =>
        Display.Scenario("A shared chore is listed once and done when anyone did it")
            .Given(Created, Joined(Ann, "Ann"), Joined(Bob, "Bob"), Bins, AssignedToAll(BinsId), Done(BinsId, Bob, new DateTime(2026, 3, 18, 8, 0, 0)))
            .ThenView("Display Today", Today, new { chores = new object[] { new { title = "Bins", state = "done", by = "Bob" } } });

    [Fact]
    public void A_chore_nobody_has_is_not_on_the_list() =>
        Display.Scenario("A chore nobody has is not on the list")
            .Given(Created, Joined(Ann, "Ann"), Dishes)
            .ThenView("Display Today", Today, new { chores = Array.Empty<object>() });

    [Fact]
    public void A_deleted_chore_is_not_on_the_list() =>
        Display.Scenario("A deleted chore is not on the list")
            .Given(Created, Joined(Ann, "Ann"), Dishes, AssignedTo(DishesId, Ann), new ChoreDeleted(DishesId, Home, Ann))
            .ThenView("Display Today", Today, new { chores = Array.Empty<object>() });

    // ── Display Lately ───────────────────────────────────────────────────────

    // A flat summary of the 40 days: the first and last day, and per person the total, today's count and day count.
    static object? Lately(IReadOnlyList<object> h)
    {
        var r = DisplayLatelyView.Project(h, Now);
        if (r == null) return null;
        return new
        {
            householdName = r.HouseholdName,
            from = r.From,
            to = r.To,
            people = r.People.Select(p => new { name = p.Name, total = p.Total, today = p.Days[^1].Count, firstDay = p.Days[0].Day, lastDay = p.Days[^1].Day, dayCount = p.Days.Count }).ToArray(),
        };
    }

    [Fact]
    public void The_forty_days_start_empty() =>
        Display.Scenario("The forty days start empty")
            .Given(Created, Joined(Ann, "Ann"))
            .ThenView("Display Lately", Lately, new
            {
                householdName = "The Smiths", from = "2026-02-07", to = "2026-03-18",
                people = new object[] { new { name = "Ann", total = 0, today = 0, firstDay = "2026-02-07", lastDay = "2026-03-18", dayCount = 40 } },
            });

    [Fact]
    public void Completions_are_counted_per_person_and_day() =>
        Display.Scenario("Completions are counted per person and day")
            .Given(Created, Joined(Ann, "Ann"), Joined(Bob, "Bob"), Dishes, Bins,
                Done(DishesId, Ann, new DateTime(2026, 3, 18, 9, 0, 0)), Done(BinsId, Ann, new DateTime(2026, 3, 18, 10, 0, 0)),
                Done(DishesId, Ann, new DateTime(2026, 3, 1, 9, 0, 0)), Done(DishesId, Bob, new DateTime(2026, 3, 17, 9, 0, 0)))
            .ThenView("Display Lately", Lately, new
            {
                people = new object[]
                {
                    new { name = "Ann", total = 3, today = 2 },
                    new { name = "Bob", total = 1, today = 0 },
                },
            });

    [Fact]
    public void Completions_older_than_forty_days_are_not_counted() =>
        Display.Scenario("Completions older than forty days are not counted")
            .Given(Created, Joined(Ann, "Ann"), Dishes, Done(DishesId, Ann, new DateTime(2026, 2, 6, 9, 0, 0)), Done(DishesId, Ann, new DateTime(2026, 2, 7, 9, 0, 0)))
            .ThenView("Display Lately", Lately, new { people = new object[] { new { name = "Ann", total = 1 } } });

    [Fact]
    public void A_removed_member_is_not_shown() =>
        Display.Scenario("A removed member is not shown")
            .Given(Created, Joined(Ann, "Ann"), Joined(Bob, "Bob"), new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("Display Lately", Lately, new { people = new object[] { new { name = "Ann" } } });

    [Fact]
    public void A_renamed_member_shows_the_new_name() =>
        Display.Scenario("A renamed member shows the new name")
            .Given(Created, Joined(Ann, "Ann"), new MemberNicknameChanged(Ann, Home, "Ann", "Annie"))
            .ThenView("Display Lately", Lately, new { people = new object[] { new { name = "Annie" } } });
}
