using ChoreMonkey.Core.Feature.Activity.Queries.CompletionTimeline;
using ChoreMonkey.Core.Feature.Activity.Queries.TeamOverview;
using ChoreMonkey.Core.Security;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Activity chapter, views: what the completion timeline and the team overview show for a given history. "Now" is Wednesday 2026-03-18 10:00 UTC.</summary>
public class ActivityViewSpecs
{
    static readonly HistoryChapter Activity = new("Activity");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid TidyId = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    static readonly Guid BinsId = Guid.Parse("c0000000-0000-0000-0000-000000000004");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid PauseA = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Today = Now.Date;
    static readonly DateTime Yesterday = Today.AddDays(-1);

    const int AdminPin = 1234;
    const int NewAdminPin = 5678;
    const int MemberPin = 4321;
    static readonly string AdminPinHash = PinHasher.HashPin(AdminPin);
    static readonly string NewAdminPinHash = PinHasher.HashPin(NewAdminPin);
    static readonly string MemberPinHash = PinHasher.HashPin(MemberPin);

    static readonly HouseholdCreated Household = new(Home, "Home", AdminPinHash);
    static readonly HouseholdCreated HouseholdWithMemberPin = new(Home, "Home", AdminPinHash, MemberPinHash);

    // Dishes is a daily chore that started on 1 March, so it can be overdue.
    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Laundry = new(LaundryId, Home, "Laundry", "Wash clothes", new ChoreFrequency("weekly"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Tidy = new(TidyId, Home, "Tidy", "Tidy up", new ChoreFrequency("daily"), IsOptional: true, StartDate: new DateTime(2026, 3, 1));

    // Events carry their own time; the views read it from the event, so the specs set it explicitly.
    static string At(DateTime when) => when.ToString("o");
    static readonly DateTime Feb1 = new(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar10 = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar11 = new(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar12 = new(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar13 = new(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar14 = new(2026, 3, 14, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar15 = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar16 = new(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar17 = new(2026, 3, 17, 9, 0, 0, DateTimeKind.Utc);

    static readonly ChoreCreated DishesCreatedInFeb = Dishes with { TimestampUtc = At(Feb1) };
    static readonly ChoreCreated LaundryCreatedMar14 = Laundry with { TimestampUtc = At(Mar14) };
    static MemberJoinedHousehold JoinedOn(DateTime when, Guid member, string nickname) =>
        new(member, Home, Guid.NewGuid(), nickname) { TimestampUtc = At(when) };
    static MemberJoinedHousehold AnnJoinedOn(DateTime when) => JoinedOn(when, Ann, "Ann");
    static MemberJoinedHousehold BobJoinedOn(DateTime when) => JoinedOn(when, Bob, "Bob");
    static ChoreAssigned AssignedOn(DateTime when, Guid chore, params Guid[] members) =>
        new(chore, Home, members, false) { TimestampUtc = At(when) };
    static ChoreAssigned AssignedToAllOn(DateTime when, Guid chore) =>
        new(chore, Home, null, true) { TimestampUtc = At(when) };
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);

    // Plain assignments without a time, for the views that read completions and the latest assignment only.
    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);

    // ── Completion Timeline ──────────────────────────────────────────────────

    static GetCompletionTimelineResponse Timeline(IReadOnlyList<object> h) => CompletionTimelineView.Project(h, Now);

    [Fact]
    public void The_timeline_is_empty_before_anything_happens() =>
        Activity.Scenario("The timeline is empty before anything happens")
            .Given()
            .ThenView("Completion Timeline", Timeline, new { activities = Array.Empty<object>() });

    [Fact]
    public void A_completion_says_who_did_which_chore_newest_first() =>
        Activity.Scenario("A completion says who did which chore, newest first")
            .Given(DishesCreatedInFeb, AnnJoinedOn(Mar10), Done(DishesId, Ann, Mar16), Done(DishesId, Ann, Mar17))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new object[]
                {
                    new { type = "completion", description = "Ann completed Dishes", choreId = DishesId, memberId = Ann },
                    new { type = "completion", description = "Ann completed Dishes", choreId = DishesId, memberId = Ann },
                    new { type = "member_joined", description = "Ann joined the household", memberId = Ann },
                },
            });

    [Fact]
    public void A_chore_for_everyone_says_so() =>
        Activity.Scenario("A chore for everyone says so")
            .Given(DishesCreatedInFeb, AssignedToAllOn(Mar16, DishesId))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new[] { new { type = "assignment", description = "Dishes assigned to everyone", choreId = DishesId } },
            });

    [Fact]
    public void An_assignment_names_who_it_is_for() =>
        Activity.Scenario("An assignment names who it is for")
            .Given(DishesCreatedInFeb, AnnJoinedOn(Mar10), BobJoinedOn(Mar11), AssignedOn(Mar16, DishesId, Ann, Bob))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new object[]
                {
                    new { type = "assignment", description = "Dishes assigned to Ann, Bob" },
                    new { type = "member_joined", description = "Bob joined the household" },
                    new { type = "member_joined", description = "Ann joined the household" },
                },
            });

    [Fact]
    public void A_chore_with_nobody_on_it_says_it_is_unassigned() =>
        Activity.Scenario("A chore with nobody on it says it is unassigned")
            .Given(DishesCreatedInFeb, new ChoreAssigned(DishesId, Home, null, false) { TimestampUtc = At(Mar16) })
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new[] { new { type = "assignment", description = "Dishes unassigned" } },
            });

    [Fact]
    public void A_name_change_shows_in_the_timeline_and_on_later_completions() =>
        Activity.Scenario("A name change shows in the timeline and on later completions")
            .Given(DishesCreatedInFeb, AnnJoinedOn(Mar10), new MemberNicknameChanged(Ann, Home, "Ann", "Annie") { TimestampUtc = At(Mar12) }, Done(DishesId, Ann, Mar17))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new object[]
                {
                    new { type = "completion", description = "Annie completed Dishes" },
                    new { type = "nickname_change", description = "Ann changed name to Annie", memberId = Ann },
                    new { type = "member_joined", description = "Ann joined the household" },
                },
            });

    [Fact]
    public void A_status_update_shows_in_the_timeline() =>
        Activity.Scenario("A status update shows in the timeline")
            .Given(AnnJoinedOn(Mar10), new MemberStatusChanged(Ann, Home, "On holiday") { TimestampUtc = At(Mar15) })
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new object[]
                {
                    new { type = "status_change", description = "Ann updated status: \"On holiday\"", memberId = Ann },
                    new { type = "member_joined", description = "Ann joined the household" },
                },
            });

    [Fact]
    public void A_removed_member_shows_as_removed() =>
        Activity.Scenario("A removed member shows as removed")
            .Given(AnnJoinedOn(Mar10), BobJoinedOn(Mar11), new MemberRemoved(Home, Bob, Ann, "Bob") { TimestampUtc = At(Mar14) })
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new object[]
                {
                    new { type = "member_removed", description = "Bob was removed from the household", memberId = Bob },
                    new { type = "member_joined", description = "Bob joined the household" },
                    new { type = "member_joined", description = "Ann joined the household" },
                },
            });

    [Fact]
    public void A_new_chore_shows_when_it_was_added() =>
        Activity.Scenario("A new chore shows when it was added")
            .Given(LaundryCreatedMar14)
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new[] { new { type = "chore_created", description = "New chore added: Laundry", choreId = LaundryId } },
            });

    [Fact]
    public void Activity_older_than_thirty_days_is_left_out() =>
        Activity.Scenario("Activity older than thirty days is left out")
            .Given(DishesCreatedInFeb, AnnJoinedOn(Feb1), Done(DishesId, Ann, Feb1), Done(DishesId, Ann, Mar16))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new[] { new { type = "completion", description = "Ann completed Dishes" } },
            });

    [Fact]
    public void A_chore_or_member_with_no_record_shows_as_unknown() =>
        Activity.Scenario("A chore or member with no record shows as unknown")
            .Given(Done(LaundryId, Ann, Mar16))
            .ThenView("Completion Timeline", Timeline, new
            {
                activities = new[] { new { type = "completion", description = "Unknown completed Unknown chore" } },
            });

    [Fact]
    public void An_event_with_an_unreadable_time_is_left_out() =>
        Activity.Scenario("An event with an unreadable time is left out")
            .Given(DishesCreatedInFeb, new ChoreAssigned(DishesId, Home, null, true) { TimestampUtc = "not a date" })
            .ThenView("Completion Timeline", Timeline, new { activities = Array.Empty<object>() });

    // ── Team Overview ────────────────────────────────────────────────────────

    static Func<IReadOnlyList<object>, TeamOverviewResponse?> Team(int pin) => h => TeamOverviewView.Project(h, pin, Now);

    [Fact]
    public void Without_a_household_there_is_no_team() =>
        Activity.Scenario("Without a household there is no team")
            .Given()
            .ThenView("Team Overview", Team(AdminPin), null);

    [Fact]
    public void A_wrong_pin_sees_no_team() =>
        Activity.Scenario("A wrong PIN sees no team")
            .Given(Household, AnnJoinedOn(Mar10), Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Team Overview", Team(9999), null);

    [Fact]
    public void The_member_pin_sees_the_team() =>
        Activity.Scenario("The member PIN sees the team")
            .Given(HouseholdWithMemberPin, AnnJoined(), Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Team Overview", Team(MemberPin), new
            {
                members = new[] { new { memberId = Ann, nickname = "Ann", totalChores = 1, overdueCount = 1 } },
            });

    [Fact]
    public void A_changed_admin_pin_opens_the_team() =>
        Activity.Scenario("A changed admin PIN opens the team")
            .Given(Household, new AdminPinChanged(Home, NewAdminPinHash), AnnJoined())
            .ThenView("Team Overview", Team(NewAdminPin), new
            {
                members = new[] { new { memberId = Ann, nickname = "Ann" } },
            });

    [Fact]
    public void The_old_admin_pin_stops_opening_the_team_once_changed() =>
        Activity.Scenario("The old admin PIN stops opening the team once changed")
            .Given(Household, new AdminPinChanged(Home, NewAdminPinHash), AnnJoined())
            .ThenView("Team Overview", Team(AdminPin), null);

    [Fact]
    public void Overdue_members_are_listed_first_with_each_chores_status() =>
        Activity.Scenario("Overdue members are listed first, with each chore's status")
            .Given(Household, AnnJoined(), BobJoinedOn(Mar10), Dishes, AssignedTo(DishesId, Ann, Bob), Done(DishesId, Ann, Now))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new object[]
                {
                    new { memberId = Bob, overdueCount = 1, chores = new[] { new { choreId = DishesId, status = "overdue", overduePeriod = "yesterday" } } },
                    new { memberId = Ann, totalChores = 1, completedCount = 1, overdueCount = 0, chores = new[] { new { choreId = DishesId, status = "completed" } } },
                },
            });

    [Fact]
    public void A_members_chores_are_listed_overdue_then_pending_then_done() =>
        Activity.Scenario("A member's chores are listed overdue, then pending, then done")
            .Given(Household, AnnJoined(), Dishes, Tidy, Laundry, AssignedTo(DishesId, Ann), AssignedTo(TidyId, Ann), AssignedTo(LaundryId, Ann), Done(LaundryId, Ann, Now))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[]
                {
                    new
                    {
                        memberId = Ann, totalChores = 3, completedCount = 1, overdueCount = 1,
                        chores = new object[]
                        {
                            new { choreId = DishesId, status = "overdue" },
                            new { choreId = TidyId, status = "pending" },
                            new { choreId = LaundryId, status = "completed" },
                        },
                    },
                },
            });

    [Fact]
    public void A_member_with_nothing_assigned_has_no_chores() =>
        Activity.Scenario("A member with nothing assigned has no chores")
            .Given(Household, AnnJoined(), BobJoinedOn(Mar10), Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new object[]
                {
                    new { memberId = Ann, overdueCount = 1 },
                    new { memberId = Bob, totalChores = 0, chores = Array.Empty<object>() },
                },
            });

    [Fact]
    public void A_one_time_chore_is_pending_until_it_is_done() =>
        Activity.Scenario("A one-time chore is pending until it is done")
            .Given(Household, AnnJoined(), Dishes with { Frequency = new ChoreFrequency("once") }, AssignedTo(DishesId, Ann))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, chores = new[] { new { choreId = DishesId, status = "pending", overduePeriod = (string?)null } } } },
            });

    [Fact]
    public void A_one_time_chore_is_completed_once_it_is_done() =>
        Activity.Scenario("A one-time chore is completed once it is done")
            .Given(Household, AnnJoined(), Dishes with { Frequency = new ChoreFrequency("once") }, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Yesterday))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, completedCount = 1, chores = new[] { new { choreId = DishesId, status = "completed" } } } },
            });

    [Fact]
    public void An_optional_chore_is_pending_never_overdue() =>
        Activity.Scenario("An optional chore is pending, never overdue")
            .Given(Household, AnnJoined(), Dishes with { IsOptional = true }, AssignedTo(DishesId, Ann))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, overdueCount = 0, chores = new[] { new { choreId = DishesId, status = "pending", overduePeriod = (string?)null } } } },
            });

    [Fact]
    public void A_paused_day_is_pending_not_overdue() =>
        Activity.Scenario("A paused day is pending, not overdue")
            .Given(Household, AnnJoined(), Dishes, AssignedTo(DishesId, Ann), new ChorePaused(PauseA, DishesId, Home, Yesterday, Yesterday, null))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, overdueCount = 0, chores = new[] { new { choreId = DishesId, status = "pending" } } } },
            });

    [Fact]
    public void A_weekly_chore_done_this_week_is_completed() =>
        Activity.Scenario("A weekly chore done this week is completed")
            .Given(Household, AnnJoined(), Laundry, AssignedTo(LaundryId, Ann), Done(LaundryId, Ann, Mar16))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, chores = new[] { new { choreId = LaundryId, status = "completed" } } } },
            });

    [Fact]
    public void A_weekly_chore_not_done_is_overdue_since_last_week() =>
        Activity.Scenario("A weekly chore not done is overdue since last week")
            .Given(Household, AnnJoined(), Laundry, AssignedTo(LaundryId, Ann))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, chores = new[] { new { choreId = LaundryId, status = "overdue", overduePeriod = "last week" } } } },
            });

    [Fact]
    public void An_interval_chore_a_couple_of_days_late_is_overdue() =>
        Activity.Scenario("An interval chore a couple of days late is overdue")
            .Given(Household, AnnJoined(), new ChoreCreated(BinsId, Home, "Bins", "Take out the bins", new ChoreFrequency("interval", null, 3), StartDate: new DateTime(2026, 3, 1)), AssignedTo(BinsId, Ann), Done(BinsId, Ann, Mar13))
            .ThenView("Team Overview", Team(AdminPin), new
            {
                members = new[] { new { memberId = Ann, chores = new[] { new { choreId = BinsId, status = "overdue", overduePeriod = "2 days ago" } } } },
            });

    static MemberJoinedHousehold AnnJoined() => new(Ann, Home, Guid.NewGuid(), "Ann");
}
