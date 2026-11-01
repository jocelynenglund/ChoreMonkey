using ChoreMonkey.Core.Feature.Chores.Queries.ChoreHistory;
using ChoreMonkey.Core.Feature.Chores.Queries.ChoreList;
using ChoreMonkey.Core.Feature.Chores.Queries.DisconnectionNotice;
using ChoreMonkey.Core.Feature.Chores.Queries.MyChores;
using ChoreMonkey.Core.Feature.Chores.Queries.OverdueChores;
using ChoreMonkey.Core.Security;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Chores chapter, views: what each read side shows for a given history. "Today" is Wednesday 2026-03-18.</summary>
public class ChoresViewSpecs
{
    static readonly HistoryChapter Chores = new("Chores");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid PauseA = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    static readonly Guid PauseB = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Today = Now.Date;
    static readonly DateTime Yesterday = Today.AddDays(-1);

    const int AdminPin = 1234;
    static readonly string AdminPinHash = PinHasher.HashPin(AdminPin);

    static readonly HouseholdCreated Household = new(Home, "Home", AdminPinHash);
    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.NewGuid(), "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");

    // Dishes is a daily chore that started on 1 March, so it can be overdue.
    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Laundry = new(LaundryId, Home, "Laundry", "Wash clothes", new ChoreFrequency("weekly"), StartDate: new DateTime(2026, 3, 1));
    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);

    // ── Chore History ────────────────────────────────────────────────────────

    static ChoreHistoryResponse History(IReadOnlyList<object> h) => ChoreHistoryView.Project(h, DishesId);

    [Fact]
    public void The_history_shows_a_chores_completions_newest_first() =>
        Chores.Scenario("The history shows a chore's completions, newest first")
            .Given(Dishes, Done(DishesId, Ann, new DateTime(2026, 3, 16, 9, 0, 0)), Done(DishesId, Bob, new DateTime(2026, 3, 17, 9, 0, 0)), Done(LaundryId, Ann, new DateTime(2026, 3, 17, 9, 0, 0)))
            .ThenView("Chore History", History, new
            {
                completions = new[]
                {
                    new { completedBy = Bob, completedAt = new DateTime(2026, 3, 17, 9, 0, 0) },
                    new { completedBy = Ann, completedAt = new DateTime(2026, 3, 16, 9, 0, 0) },
                },
            });

    [Fact]
    public void The_history_is_empty_before_anything_is_done() =>
        Chores.Scenario("The history is empty before anything is done")
            .Given(Dishes)
            .ThenView("Chore History", History, new { completions = Array.Empty<object>() });

    // ── Chore List ───────────────────────────────────────────────────────────

    static GetChoresResponse List(IReadOnlyList<object> h) => ChoreListView.Project(h, Now);

    [Fact]
    public void The_list_shows_a_chore_with_who_it_is_for_and_when_it_was_last_done() =>
        Chores.Scenario("The list shows a chore with who it is for and when it was last done")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Now))
            .ThenView("Chore List", List, new
            {
                chores = new[]
                {
                    new
                    {
                        choreId = DishesId, displayName = "Dishes", description = "Wash up",
                        assignedTo = new[] { Ann }, assignedToAll = false,
                        frequency = new { type = "daily" },
                        lastCompletedAt = Now, lastCompletedBy = Ann,
                        memberCompletions = new[] { new { memberId = Ann, completedToday = true, completedThisWeek = true } },
                        isOptional = false, isRequired = true, missedDeduction = 10m,
                    },
                },
            });

    [Fact]
    public void A_chore_with_no_frequency_shows_as_one_time() =>
        Chores.Scenario("A chore with no frequency shows as one-time")
            .Given(new ChoreCreated(DishesId, Home, "Dishes", "Wash up"))
            .ThenView("Chore List", List, new { chores = new[] { new { choreId = DishesId, frequency = new { type = "once" }, assignedToAll = false } } });

    [Fact]
    public void An_update_changes_what_the_list_shows() =>
        Chores.Scenario("An update changes what the list shows")
            .Given(Dishes, new ChoreUpdated(DishesId, Home, "Dishes and pans", "Wash up properly", new ChoreFrequency("weekly"), true, null, false, 5m))
            .ThenView("Chore List", List, new
            {
                chores = new[] { new { displayName = "Dishes and pans", description = "Wash up properly", frequency = new { type = "weekly" }, isOptional = true, isRequired = false, missedDeduction = 5m } },
            });

    [Fact]
    public void A_deleted_chore_is_not_listed() =>
        Chores.Scenario("A deleted chore is not listed")
            .Given(Dishes, Laundry, new ChoreDeleted(DishesId, Home, Guid.Empty))
            .ThenView("Chore List", List, new { chores = new[] { new { choreId = LaundryId } } });

    [Fact]
    public void A_chore_for_everyone_shows_every_member() =>
        Chores.Scenario("A chore for everyone shows every member")
            .Given(AnnJoined, BobJoined, Dishes, AssignedToAll(DishesId), Done(DishesId, Bob, Yesterday.AddHours(20)))
            .ThenView("Chore List", List, new
            {
                chores = new[]
                {
                    new
                    {
                        assignedToAll = true,
                        memberCompletions = new[]
                        {
                            new { memberId = Ann, completedToday = false, completedThisWeek = false },
                            new { memberId = Bob, completedToday = false, completedThisWeek = true },
                        },
                    },
                },
            });

    [Fact]
    public void The_list_shows_rates_and_active_pauses() =>
        Chores.Scenario("The list shows rates and active pauses")
            .Given(Dishes,
                new ChoreRatesSet(Home, DishesId, 15m, 20m, Now),
                new ChorePaused(PauseA, DishesId, Home, new DateTime(2026, 3, 1), new DateTime(2026, 3, 7), "Holiday"),
                new ChorePaused(PauseB, DishesId, Home, new DateTime(2026, 4, 1), new DateTime(2026, 4, 2), null))
            .ThenView("Chore List", List, new
            {
                chores = new[]
                {
                    new
                    {
                        deductionRate = 15m, bonusRate = 20m,
                        pauses = new[]
                        {
                            new { pauseId = PauseA, start = new DateTime(2026, 3, 1), end = new DateTime(2026, 3, 7), reason = (string?)"Holiday" },
                            new { pauseId = PauseB, start = new DateTime(2026, 4, 1), end = new DateTime(2026, 4, 2), reason = (string?)null },
                        },
                    },
                },
            });

    [Fact]
    public void A_removed_pause_is_not_shown() =>
        Chores.Scenario("A removed pause is not shown")
            .Given(Dishes,
                new ChorePaused(PauseA, DishesId, Home, new DateTime(2026, 3, 1), new DateTime(2026, 3, 7), "Holiday"),
                new ChorePauseRemoved(PauseA, DishesId, Home))
            .ThenView("Chore List", List, new { chores = new[] { new { choreId = DishesId, pauses = (object?)null } } });

    // ── My Chores ────────────────────────────────────────────────────────────

    static GetMyChoresResponse? Mine(IReadOnlyList<object> h) => MyChoresView.Project(h, Ann, Now);

    [Fact]
    public void A_chore_assigned_to_me_is_pending() =>
        Chores.Scenario("A chore assigned to me is pending")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Yesterday.AddHours(9)))
            .ThenView("My Chores", Mine, new
            {
                pending = new[] { new { choreId = DishesId, displayName = "Dishes", frequencyType = "daily", dueDescription = "today" } },
                overdue = Array.Empty<object>(),
                completed = Array.Empty<object>(),
            });

    [Fact]
    public void A_chore_done_today_is_completed() =>
        Chores.Scenario("A chore done today is completed")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Yesterday.AddHours(9)), Done(DishesId, Ann, Now))
            .ThenView("My Chores", Mine, new
            {
                pending = Array.Empty<object>(),
                overdue = Array.Empty<object>(),
                completed = new[] { new { choreId = DishesId, displayName = "Dishes", completedAt = Now } },
            });

    [Fact]
    public void A_chore_missed_yesterday_is_overdue() =>
        Chores.Scenario("A chore missed yesterday is overdue")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann))
            .ThenView("My Chores", Mine, new
            {
                pending = new[] { new { choreId = DishesId } },
                overdue = new[] { new { choreId = DishesId, displayName = "Dishes", frequencyType = "daily", overduePeriod = "yesterday", periodKey = "2026-03-17" } },
            });

    [Fact]
    public void An_acknowledged_miss_is_no_longer_overdue() =>
        Chores.Scenario("An acknowledged miss is no longer overdue")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann), new ChoreMissedAcknowledged(DishesId, Ann, "2026-03-17"))
            .ThenView("My Chores", Mine, new { overdue = Array.Empty<object>() });

    [Fact]
    public void A_paused_day_is_not_overdue() =>
        Chores.Scenario("A paused day is not overdue")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann), new ChorePaused(PauseA, DishesId, Home, Yesterday, Yesterday, null))
            .ThenView("My Chores", Mine, new { overdue = Array.Empty<object>() });

    [Fact]
    public void A_weekly_chore_missed_last_week_is_overdue() =>
        Chores.Scenario("A weekly chore missed last week is overdue")
            .Given(AnnJoined, Laundry, AssignedTo(LaundryId, Ann))
            .ThenView("My Chores", Mine, new
            {
                pending = new[] { new { choreId = LaundryId, dueDescription = "this week" } },
                overdue = new[] { new { choreId = LaundryId, overduePeriod = "last week", periodKey = "2026-W11" } },
            });

    [Fact]
    public void An_optional_chore_is_never_overdue() =>
        Chores.Scenario("An optional chore is never overdue")
            .Given(AnnJoined, Dishes with { IsOptional = true }, AssignedTo(DishesId, Ann))
            .ThenView("My Chores", Mine, new { pending = new[] { new { choreId = DishesId } }, overdue = Array.Empty<object>() });

    [Fact]
    public void A_chore_for_someone_else_is_not_mine() =>
        Chores.Scenario("A chore for someone else is not mine")
            .Given(AnnJoined, BobJoined, Dishes, AssignedTo(DishesId, Bob))
            .ThenView("My Chores", Mine, new { pending = Array.Empty<object>(), overdue = Array.Empty<object>(), completed = Array.Empty<object>() });

    [Fact]
    public void A_shared_chore_done_by_someone_else_says_who() =>
        Chores.Scenario("A shared chore done by someone else says who")
            .Given(AnnJoined, BobJoined, new MemberNicknameChanged(Bob, Home, "Bob", "Bobby"), Dishes, AssignedToAll(DishesId), Done(DishesId, Bob, Now))
            .ThenView("My Chores", Mine, new
            {
                pending = Array.Empty<object>(),
                completed = new[] { new { choreId = DishesId, completedByName = "Bobby" } },
            });

    [Fact]
    public void An_unknown_member_has_no_chores() =>
        Chores.Scenario("An unknown member has no chores")
            .Given(Dishes, AssignedTo(DishesId, Ann))
            .ThenView("My Chores", Mine, null);

    // ── Overdue Chores ───────────────────────────────────────────────────────

    static GetOverdueResponse? Overdue(IReadOnlyList<object> h) => OverdueChoresView.Project(h, AdminPin, Now);

    [Fact]
    public void Nobody_is_overdue_when_everything_was_done() =>
        Chores.Scenario("Nobody is overdue when everything was done")
            .Given(Household, AnnJoined, BobJoined, Dishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Yesterday.AddHours(9)))
            .ThenView("Overdue Chores", Overdue, new
            {
                memberOverdue = new[]
                {
                    new { memberId = Ann, nickname = "Ann", overdueCount = 0, chores = Array.Empty<object>() },
                    new { memberId = Bob, nickname = "Bob", overdueCount = 0, chores = Array.Empty<object>() },
                },
            });

    [Fact]
    public void A_member_who_missed_yesterday_is_listed_first() =>
        Chores.Scenario("A member who missed yesterday is listed first")
            .Given(Household, AnnJoined, BobJoined, Dishes, AssignedTo(DishesId, Ann, Bob), Done(DishesId, Bob, Yesterday.AddHours(9)))
            .ThenView("Overdue Chores", Overdue, new
            {
                memberOverdue = new[]
                {
                    new { memberId = Ann, nickname = "Ann", overdueCount = 1 },
                    new { memberId = Bob, nickname = "Bob", overdueCount = 0 },
                },
            });

    [Fact]
    public void The_overdue_chore_says_when_it_was_missed() =>
        Chores.Scenario("The overdue chore says when it was missed")
            .Given(Household, AnnJoined, Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Overdue Chores", Overdue, new
            {
                memberOverdue = new[] { new { memberId = Ann, chores = new[] { new { choreId = DishesId, displayName = "Dishes", overduePeriod = "yesterday" } } } },
            });

    [Fact]
    public void An_optional_chore_is_not_on_the_overdue_list() =>
        Chores.Scenario("An optional chore is not on the overdue list")
            .Given(Household, AnnJoined, Dishes with { IsOptional = true }, AssignedTo(DishesId, Ann))
            .ThenView("Overdue Chores", Overdue, new { memberOverdue = new[] { new { memberId = Ann, overdueCount = 0 } } });

    [Fact]
    public void A_paused_chore_is_not_overdue_for_the_admin() =>
        Chores.Scenario("A paused day is not overdue for the admin")
            .Given(Household, AnnJoined, Dishes, AssignedTo(DishesId, Ann), new ChorePaused(PauseA, DishesId, Home, Yesterday, Yesterday, null))
            .ThenView("Overdue Chores", Overdue, new { memberOverdue = new[] { new { memberId = Ann, overdueCount = 0 } } });

    [Fact]
    public void A_wrong_pin_sees_nothing() =>
        Chores.Scenario("A wrong PIN sees nothing")
            .Given(Household, AnnJoined, Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Overdue Chores", h => OverdueChoresView.Project(h, 9999, Now), null);

    [Fact]
    public void Without_a_household_there_is_nothing_to_see() =>
        Chores.Scenario("Without a household there is nothing to see")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Overdue Chores", Overdue, null);

    // ── Disconnection Notice ─────────────────────────────────────────────────

    static GetDisconnectionNoticeResponse? Notice(IReadOnlyList<object> h) => DisconnectionNoticeView.Project(h, Ann, Now);

    // A daily chore that started in January: more than two months before today.
    static readonly ChoreCreated OldDishes = Dishes with { StartDate = new DateTime(2026, 1, 1) };

    [Fact]
    public void Two_months_of_nothing_done_is_a_notice_of_disconnection() =>
        Chores.Scenario("Two months of nothing done is a notice of disconnection")
            .Given(AnnJoined, OldDishes, AssignedTo(DishesId, Ann))
            .ThenView("Disconnection Notice", Notice, new
            {
                isDisconnected = true,
                windowStart = new DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc),
                missedChores = new[] { new { choreId = DishesId, displayName = "Dishes", frequencyType = "daily" } },
            });

    [Fact]
    public void Doing_one_chore_in_the_window_lifts_the_notice() =>
        Chores.Scenario("Doing one chore in the window lifts the notice")
            .Given(AnnJoined, OldDishes, AssignedTo(DishesId, Ann), Done(DishesId, Ann, Yesterday))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false, missedChores = Array.Empty<object>() });

    [Fact]
    public void A_shared_chore_done_by_anyone_counts() =>
        Chores.Scenario("A shared chore done by anyone counts")
            .Given(AnnJoined, BobJoined, OldDishes, AssignedToAll(DishesId), Done(DishesId, Bob, Yesterday))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false });

    [Fact]
    public void A_chore_that_started_inside_the_window_is_not_enough() =>
        Chores.Scenario("A chore that started inside the window is not enough")
            .Given(AnnJoined, Dishes, AssignedTo(DishesId, Ann))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false, missedChores = Array.Empty<object>() });

    [Fact]
    public void An_optional_chore_is_not_obligatory() =>
        Chores.Scenario("An optional chore is not obligatory")
            .Given(AnnJoined, OldDishes with { IsOptional = true }, AssignedTo(DishesId, Ann))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false });

    [Fact]
    public void A_one_time_chore_is_not_obligatory() =>
        Chores.Scenario("A one-time chore is not obligatory")
            .Given(AnnJoined, OldDishes with { Frequency = new ChoreFrequency("once") }, AssignedTo(DishesId, Ann))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false });

    [Fact]
    public void A_pause_in_the_window_excuses_the_chore() =>
        Chores.Scenario("A pause in the window excuses the chore")
            .Given(AnnJoined, OldDishes, AssignedTo(DishesId, Ann), new ChorePaused(PauseA, DishesId, Home, Today.AddDays(-10), Today.AddDays(-5), null))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false });

    [Fact]
    public void A_chore_for_someone_else_is_not_mine_to_miss() =>
        Chores.Scenario("A chore for someone else is not mine to miss")
            .Given(AnnJoined, BobJoined, OldDishes, AssignedTo(DishesId, Bob))
            .ThenView("Disconnection Notice", Notice, new { isDisconnected = false });

    [Fact]
    public void An_unknown_member_has_no_notice() =>
        Chores.Scenario("An unknown member has no notice")
            .Given(OldDishes, AssignedTo(DishesId, Ann))
            .ThenView("Disconnection Notice", Notice, null);
}
