using ChoreMonkey.Core.Feature.Salary.Queries.GetAvailablePeriods;
using ChoreMonkey.Core.Feature.Salary.Queries.GetCurrentPeriod;
using ChoreMonkey.Core.Feature.Salary.Queries.GetOfficialSalarySlip;
using ChoreMonkey.Core.Feature.Salary.Queries.GetPayoutHistory;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Salary chapter, views: what each read side shows for a given history. "Today" is Wednesday 2026-03-18 unless a
/// scenario says otherwise; with payday 25 that is inside the pay period 26 Feb - 25 Mar, and a missed chore only
/// counts once it is more than two days behind (so the last day counted is 16 March).
/// </summary>
public class SalaryViewSpecs
{
    static readonly HistoryChapter Salary = new("Salary");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid PauseA = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    static readonly Guid PeriodOne = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    static readonly Guid PeriodTwo = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

    static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.NewGuid(), "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");
    static MemberSalarySet SalaryOf(Guid member, decimal baseSalary, decimal deduction = 1m, decimal bonus = 1m) =>
        new(Home, member, baseSalary, deduction, bonus, Now);
    static ChoreCreated Daily(Guid id, string name, DateTime? start = null, decimal missed = 10m) =>
        new(id, Home, name, "", new ChoreFrequency("daily"), StartDate: start, MissedDeduction: missed);
    static ChoreCreated Optional(Guid id, string name) =>
        new(id, Home, name, "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false);
    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);
    static ChoreRatesSet Rates(Guid chore, decimal? deduction, decimal? bonus) => new(Home, chore, deduction, bonus, Now);
    static ChorePaused Pause(Guid chore, DateTime from, DateTime to) => new(PauseA, chore, Home, from, to, null);

    // ── Current Period ───────────────────────────────────────────────────────

    static CurrentPeriodResponse Current(IReadOnlyList<object> h) => CurrentPeriodView.Project(h, null, Now);
    static Func<IReadOnlyList<object>, CurrentPeriodResponse> CurrentAt(DateTime at) => h => CurrentPeriodView.Project(h, null, at);
    static Func<IReadOnlyList<object>, CurrentPeriodResponse> CurrentFor(Guid member) => h => CurrentPeriodView.Project(h, member, Now);

    static object Missed(Guid chore, string name, string period, decimal deduction) =>
        new { choreId = chore, choreName = name, period, deduction };

    [Fact]
    public void The_current_period_with_nobody_in_the_household_is_empty() =>
        Salary.Scenario("The current period with nobody in the household is empty")
            .Given()
            .ThenView("Current Period", Current, new { periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25), members = Array.Empty<object>() });

    [Fact]
    public void A_member_is_projected_their_base_salary() =>
        Salary.Scenario("A member is projected their base salary")
            .Given(AnnJoined, SalaryOf(Ann, 1000m, deduction: 1.5m, bonus: 2m))
            .ThenView("Current Period", Current, new
            {
                periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25),
                members = new[]
                {
                    new
                    {
                        memberId = Ann, name = "Ann", baseSalary = 1000m, deductionMultiplier = 1.5m, bonusMultiplier = 2m,
                        deductions = 0m, bonuses = 0m, projected = 1000m,
                        missedChores = Array.Empty<object>(), bonusChores = Array.Empty<object>(),
                    },
                },
            });

    [Fact]
    public void A_member_with_no_salary_set_shows_zero_and_neutral_multipliers() =>
        Salary.Scenario("A member with no salary set shows zero and neutral multipliers")
            .Given(AnnJoined)
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { memberId = Ann, baseSalary = 0m, deductionMultiplier = 1m, bonusMultiplier = 1m, projected = 0m } },
            });

    [Fact]
    public void The_latest_salary_setting_is_the_one_shown() =>
        Salary.Scenario("The latest salary setting is the one shown")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), SalaryOf(Ann, 1200m, 2m, 3m))
            .ThenView("Current Period", Current, new { members = new[] { new { baseSalary = 1200m, deductionMultiplier = 2m, bonusMultiplier = 3m, projected = 1200m } } });

    [Fact]
    public void Members_are_shown_under_their_current_names_and_removed_ones_are_left_out() =>
        Salary.Scenario("Members are shown under their current names, removed ones left out")
            .Given(AnnJoined, BobJoined, new MemberNicknameChanged(Ann, Home, "Ann", "Annie"), new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("Current Period", Current, new { members = new[] { new { memberId = Ann, name = "Annie" } } });

    [Fact]
    public void Asking_for_one_member_shows_only_them() =>
        Salary.Scenario("Asking for one member shows only them")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 1000m), SalaryOf(Bob, 500m))
            .ThenView("Current Period", CurrentFor(Bob), new { members = new[] { new { memberId = Bob, name = "Bob", projected = 500m } } });

    [Fact]
    public void Asking_for_someone_who_is_not_a_member_shows_nobody() =>
        Salary.Scenario("Asking for someone who is not a member shows nobody")
            .Given(AnnJoined, SalaryOf(Ann, 1000m))
            .ThenView("Current Period", CurrentFor(Bob), new { periodStart = Utc(2026, 2, 26), members = Array.Empty<object>() });

    // Missed required chores

    [Fact]
    public void A_missed_day_more_than_two_days_back_is_deducted() =>
        Salary.Scenario("A missed day more than two days back is deducted")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 14)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        memberId = Ann, deductions = 30m, bonuses = 0m, projected = 970m,
                        missedChores = new[]
                        {
                            Missed(DishesId, "Dishes", "2026-03-14", 10m),
                            Missed(DishesId, "Dishes", "2026-03-15", 10m),
                            Missed(DishesId, "Dishes", "2026-03-16", 10m),
                        },
                    },
                },
            });

    [Fact]
    public void The_two_day_grace_period_protects_the_last_two_days() =>
        Salary.Scenario("The two-day grace period protects yesterday and the day before")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 17)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, projected = 1000m, missedChores = Array.Empty<object>() } } });

    [Fact]
    public void The_16th_is_the_last_day_counted() =>
        Salary.Scenario("The 16th is the first day to count as missed")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 10m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-16", 10m) } } } });

    [Fact]
    public void A_completed_day_is_not_missed() =>
        Salary.Scenario("A completed day is not missed")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 14)), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 15, 20, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        deductions = 20m, projected = 980m,
                        missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-14", 10m), Missed(DishesId, "Dishes", "2026-03-16", 10m) },
                    },
                },
            });

    [Fact]
    public void A_chore_older_than_the_period_counts_from_the_period_start() =>
        Salary.Scenario("A chore older than the period counts from the period start")
            // 26 Feb to 16 Mar is 19 days.
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 1, 1), missed: 1m), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", h => new
            {
                days = Current(h).Members[0].MissedChores.Count,
                first = Current(h).Members[0].MissedChores[0].Period,
                last = Current(h).Members[0].MissedChores[^1].Period,
                projected = Current(h).Members[0].Projected,
            }, new { days = 19, first = "2026-02-26", last = "2026-03-16", projected = 981m });

    [Fact]
    public void A_chore_with_no_start_date_counts_from_the_period_start() =>
        Salary.Scenario("A chore with no start date counts from the period start")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", missed: 1m), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", h => new { days = Current(h).Members[0].MissedChores.Count }, new { days = 19 });

    [Fact]
    public void The_members_multiplier_scales_the_deduction() =>
        Salary.Scenario("The member's multiplier scales the deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m, deduction: 1.5m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 15m, projected = 985m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-16", 15m) } } },
            });

    [Fact]
    public void A_chore_rate_overrides_the_choresown_deduction() =>
        Salary.Scenario("A chore rate overrides the chore's own deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedTo(DishesId, Ann), Rates(DishesId, 2.5m, null))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 2.5m, projected = 997.5m } } });

    [Fact]
    public void A_cleared_chore_rate_falls_back_to_the_choresown_deduction() =>
        Salary.Scenario("A cleared chore rate falls back to the chore's own deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16), missed: 7m), AssignedTo(DishesId, Ann),
                Rates(DishesId, 2.5m, null), Rates(DishesId, null, null))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 7m, projected = 993m } } });

    [Fact]
    public void Projected_pay_never_goes_below_zero() =>
        Salary.Scenario("Projected pay never goes below zero")
            .Given(AnnJoined, SalaryOf(Ann, 25m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 14)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new { members = new[] { new { baseSalary = 25m, deductions = 30m, projected = 0m } } });

    [Fact]
    public void A_chore_assigned_to_everyone_counts_for_each_member() =>
        Salary.Scenario("A chore assigned to everyone counts for each member")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 100m), SalaryOf(Bob, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedToAll(DishesId),
                Done(DishesId, Bob, new DateTime(2026, 3, 16, 8, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { memberId = Ann, deductions = 10m, projected = 90m }, new { memberId = Bob, deductions = 0m, projected = 100m } },
            });

    [Fact]
    public void A_chore_assigned_to_someone_else_does_not_count() =>
        Salary.Scenario("A chore assigned to someone else doesn't count")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 100m), SalaryOf(Bob, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedTo(DishesId, Bob))
            .ThenView("Current Period", Current, new { members = new[] { new { memberId = Ann, deductions = 0m }, new { memberId = Bob, deductions = 10m } } });

    [Fact]
    public void An_unassigned_chore_does_not_count() =>
        Salary.Scenario("An unassigned chore doesn't count")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, projected = 100m } } });

    [Fact]
    public void A_deleted_chore_does_not_count() =>
        Salary.Scenario("A deleted chore doesn't count")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 16)), AssignedTo(DishesId, Ann), new ChoreDeleted(DishesId, Home, Ann))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, projected = 100m } } });

    [Fact]
    public void An_update_changes_what_the_chore_costs() =>
        Salary.Scenario("An update changes what the chore costs")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 14)), AssignedTo(DishesId, Ann),
                new ChoreUpdated(DishesId, Home, "Plates", "", new ChoreFrequency("daily"), false, Utc(2026, 3, 16), true, 4m))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 4m, projected = 96m, missedChores = new[] { Missed(DishesId, "Plates", "2026-03-16", 4m) } } },
            });

    [Fact]
    public void A_one_time_chore_never_counts_as_missed() =>
        Salary.Scenario("A one-time chore never counts as missed")
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("once"), StartDate: Utc(2026, 3, 1)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, projected = 100m } } });

    // Pauses (PR #55)

    [Fact]
    public void A_pause_exempts_the_days_it_covers() =>
        Salary.Scenario("A pause exempts the days it covers, both ends included")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 10)), AssignedTo(DishesId, Ann), Pause(DishesId, Utc(2026, 3, 12), Utc(2026, 3, 13)))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        deductions = 50m, projected = 50m,
                        missedChores = new[]
                        {
                            Missed(DishesId, "Dishes", "2026-03-10", 10m), Missed(DishesId, "Dishes", "2026-03-11", 10m),
                            Missed(DishesId, "Dishes", "2026-03-14", 10m), Missed(DishesId, "Dishes", "2026-03-15", 10m),
                            Missed(DishesId, "Dishes", "2026-03-16", 10m),
                        },
                    },
                },
            });

    [Fact]
    public void A_removed_pause_exempts_nothing() =>
        Salary.Scenario("A removed pause exempts nothing")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 10)), AssignedTo(DishesId, Ann),
                Pause(DishesId, Utc(2026, 3, 12), Utc(2026, 3, 13)), new ChorePauseRemoved(PauseA, DishesId, Home))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 70m, projected = 30m } } });

    [Fact]
    public void A_pause_that_began_before_the_period_exempts_the_days_inside_it() =>
        Salary.Scenario("A pause that began before the period exempts the days inside it")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 10)), AssignedTo(DishesId, Ann), Pause(DishesId, Utc(2026, 2, 20), Utc(2026, 3, 12)))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        deductions = 40m,
                        missedChores = new[]
                        {
                            Missed(DishesId, "Dishes", "2026-03-13", 10m), Missed(DishesId, "Dishes", "2026-03-14", 10m),
                            Missed(DishesId, "Dishes", "2026-03-15", 10m), Missed(DishesId, "Dishes", "2026-03-16", 10m),
                        },
                    },
                },
            });

    [Fact]
    public void A_pause_running_past_today_exempts_the_days_up_to_the_grace_cutoff() =>
        Salary.Scenario("A pause running past today exempts the later days")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 12)), AssignedTo(DishesId, Ann), Pause(DishesId, Utc(2026, 3, 15), Utc(2026, 3, 31)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 30m, projected = 70m } } });

    [Fact]
    public void A_pause_on_another_chore_exempts_nothing() =>
        Salary.Scenario("A pause on another chore exempts nothing")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 14)), AssignedTo(DishesId, Ann), Pause(LaundryId, Utc(2026, 3, 1), Utc(2026, 3, 31)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 30m } } });

    [Fact]
    public void A_pause_covering_every_missed_day_leaves_the_pay_untouched() =>
        Salary.Scenario("A pause covering every missed day leaves the pay untouched")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Daily(DishesId, "Dishes", start: Utc(2026, 3, 10)), AssignedTo(DishesId, Ann), Pause(DishesId, Utc(2026, 3, 1), Utc(2026, 3, 20)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, projected = 100m, missedChores = Array.Empty<object>() } } });

    // Weekly and interval chores

    [Fact]
    public void A_weekly_chore_on_set_days_misses_each_day_that_went_undone() =>
        Salary.Scenario("A weekly chore on set days misses each such day that went undone")
            // Mondays from 26 Feb to 16 Mar: 2, 9 and 16 March.
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly", ["monday"])), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 9, 8, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 20m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-02", 10m), Missed(DishesId, "Dishes", "2026-03-16", 10m) } } },
            });

    [Fact]
    public void A_pause_exempts_a_set_day_it_covers() =>
        Salary.Scenario("A pause exempts a set day it covers")
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly", ["monday"])), AssignedTo(DishesId, Ann),
                Pause(DishesId, Utc(2026, 3, 9), Utc(2026, 3, 9)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 20m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-02", 10m), Missed(DishesId, "Dishes", "2026-03-16", 10m) } } },
            });

    [Fact]
    public void A_weekly_chore_with_no_days_misses_each_whole_week_that_has_ended() =>
        Salary.Scenario("A weekly chore with no days misses each whole week that has ended")
            // Weeks starting Mon 2 and 9 March have ended by the cutoff; the week starting 16 March has not.
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly"), StartDate: Utc(2026, 3, 2)), AssignedTo(DishesId, Ann))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 20m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-W10", 10m), Missed(DishesId, "Dishes", "2026-W11", 10m) } } },
            });

    [Fact]
    public void A_completion_during_a_week_covers_that_week() =>
        Salary.Scenario("A completion during a week covers that week")
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly"), StartDate: Utc(2026, 3, 2)), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 4, 8, 0, 0)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 10m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-W11", 10m) } } } });

    [Fact]
    public void A_pause_touching_any_day_of_a_week_exempts_that_whole_week() =>
        Salary.Scenario("A pause touching any day of a week exempts that whole week")
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly"), StartDate: Utc(2026, 3, 2)), AssignedTo(DishesId, Ann),
                Pause(DishesId, Utc(2026, 3, 8), Utc(2026, 3, 9)))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, missedChores = Array.Empty<object>() } } });

    [Fact]
    public void An_interval_chore_is_missed_each_time_it_falls_due_undone() =>
        Salary.Scenario("An interval chore is missed each time it falls due undone")
            // Done 1 March, due every 4 days: 5, 9 and 13 March are missed; the 17th is still ahead of the cutoff.
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("interval", IntervalDays: 4)), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 1, 8, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        deductions = 30m,
                        missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-05", 10m), Missed(DishesId, "Dishes", "2026-03-09", 10m), Missed(DishesId, "Dishes", "2026-03-13", 10m) },
                    },
                },
            });

    [Fact]
    public void A_pause_exempts_an_interval_due_date_but_the_schedule_carries_on() =>
        Salary.Scenario("A pause exempts an interval due date but the schedule carries on")
            .Given(AnnJoined, SalaryOf(Ann, 100m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("interval", IntervalDays: 4)), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 1, 8, 0, 0)), Pause(DishesId, Utc(2026, 3, 9), Utc(2026, 3, 9)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { deductions = 20m, missedChores = new[] { Missed(DishesId, "Dishes", "2026-03-05", 10m), Missed(DishesId, "Dishes", "2026-03-13", 10m) } } },
            });

    // Bonus chores

    [Fact]
    public void An_optional_chore_done_earns_its_bonus() =>
        Salary.Scenario("An optional chore done earns its bonus")
            .Given(AnnJoined, SalaryOf(Ann, 1000m, bonus: 2m), Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Rates(LaundryId, null, 5m),
                Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)), Done(LaundryId, Ann, new DateTime(2026, 3, 12, 9, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[]
                {
                    new
                    {
                        deductions = 0m, bonuses = 20m, projected = 1020m,
                        bonusChores = new[]
                        {
                            new { choreId = LaundryId, choreName = "Laundry", completedAt = new DateTime(2026, 3, 10, 9, 0, 0), bonus = 10m },
                            new { choreId = LaundryId, choreName = "Laundry", completedAt = new DateTime(2026, 3, 12, 9, 0, 0), bonus = 10m },
                        },
                    },
                },
            });

    [Fact]
    public void An_optional_chore_not_done_costs_nothing() =>
        Salary.Scenario("An optional chore not done costs nothing")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Rates(LaundryId, null, 5m))
            .ThenView("Current Period", Current, new { members = new[] { new { deductions = 0m, bonuses = 0m, projected = 1000m, bonusChores = Array.Empty<object>() } } });

    [Fact]
    public void An_optional_chore_with_no_bonus_rate_pays_zero() =>
        Salary.Scenario("An optional chore with no bonus rate pays zero")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)))
            .ThenView("Current Period", Current, new { members = new[] { new { bonuses = 0m, projected = 1000m, bonusChores = new[] { new { choreId = LaundryId, bonus = 0m } } } } });

    [Fact]
    public void Someone_elses_bonus_chore_completion_does_not_pay_out() =>
        Salary.Scenario("Someone else's completion of a bonus chore doesn't pay out")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 100m), SalaryOf(Bob, 100m), Optional(LaundryId, "Laundry"), AssignedToAll(LaundryId), Rates(LaundryId, null, 5m),
                Done(LaundryId, Bob, new DateTime(2026, 3, 10, 9, 0, 0)))
            .ThenView("Current Period", Current, new { members = new[] { new { memberId = Ann, bonuses = 0m }, new { memberId = Bob, bonuses = 5m } } });

    [Fact]
    public void A_bonus_at_midnight_opening_the_period_counts_but_one_just_before_does_not() =>
        Salary.Scenario("A bonus at midnight opening the period counts, one just before doesn't")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Rates(LaundryId, null, 5m),
                Done(LaundryId, Ann, new DateTime(2026, 2, 25, 23, 59, 0)), Done(LaundryId, Ann, new DateTime(2026, 2, 26, 0, 0, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { bonuses = 5m, bonusChores = new[] { new { completedAt = new DateTime(2026, 2, 26, 0, 0, 0), bonus = 5m } } } },
            });

    [Fact]
    public void A_bonus_at_midnight_closing_the_period_counts_but_later_that_day_does_not() =>
        Salary.Scenario("A bonus at midnight closing the period counts, later on payday doesn't")
            .Given(AnnJoined, SalaryOf(Ann, 100m), Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Rates(LaundryId, null, 5m),
                Done(LaundryId, Ann, new DateTime(2026, 3, 25, 0, 0, 0)), Done(LaundryId, Ann, new DateTime(2026, 3, 25, 0, 1, 0)))
            .ThenView("Current Period", Current, new
            {
                members = new[] { new { bonuses = 5m, bonusChores = new[] { new { completedAt = new DateTime(2026, 3, 25, 0, 0, 0), bonus = 5m } } } },
            });

    [Fact]
    public void Deductions_and_bonuses_net_off_together() =>
        Salary.Scenario("Deductions and bonuses net off together")
            .Given(AnnJoined, SalaryOf(Ann, 100m, deduction: 0.5m, bonus: 1.5m),
                Daily(DishesId, "Dishes", start: Utc(2026, 3, 15)), AssignedTo(DishesId, Ann),
                Optional(LaundryId, "Laundry"), AssignedTo(LaundryId, Ann), Rates(LaundryId, null, 4m), Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)))
            .ThenView("Current Period", Current, new { members = new[] { new { baseSalary = 100m, deductions = 10m, bonuses = 6m, projected = 96m } } });

    // Period boundaries

    [Fact]
    public void On_payday_the_period_ending_today_is_still_the_current_one() =>
        Salary.Scenario("On payday the period ending today is still the current one")
            .Given()
            .ThenView("Current Period", CurrentAt(new DateTime(2026, 3, 25, 23, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25) });

    [Fact]
    public void The_day_after_payday_starts_the_next_period() =>
        Salary.Scenario("The day after payday starts the next period")
            .Given()
            .ThenView("Current Period", CurrentAt(new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2026, 3, 26), periodEnd = Utc(2026, 4, 25) });

    [Fact]
    public void The_period_after_a_december_payday_runs_into_january() =>
        Salary.Scenario("The period after a December payday runs into January")
            .Given()
            .ThenView("Current Period", CurrentAt(new DateTime(2025, 12, 26, 9, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2025, 12, 26), periodEnd = Utc(2026, 1, 25) });

    [Fact]
    public void Early_in_january_the_period_began_in_december() =>
        Salary.Scenario("Early in January the period began in December")
            .Given()
            .ThenView("Current Period", CurrentAt(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2025, 12, 26), periodEnd = Utc(2026, 1, 25) });

    [Fact]
    public void The_configured_payday_moves_the_period() =>
        Salary.Scenario("The configured payday moves the period")
            .Given(new PaydayConfigured(Home, 10))
            .ThenView("Current Period", Current, new { periodStart = Utc(2026, 3, 11), periodEnd = Utc(2026, 4, 10) });

    [Fact]
    public void A_payday_on_the_28th_ends_a_february_period_on_the_28th() =>
        Salary.Scenario("A payday on the 28th ends a February period on the 28th")
            .Given(new PaydayConfigured(Home, 28))
            .ThenView("Current Period", CurrentAt(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2026, 1, 29), periodEnd = Utc(2026, 2, 28) });

    [Fact]
    public void A_payday_on_the_28th_starts_the_next_period_on_the_first_of_march() =>
        Salary.Scenario("A payday on the 28th starts the next period on 1 March")
            .Given(new PaydayConfigured(Home, 28))
            .ThenView("Current Period", CurrentAt(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)), new { periodStart = Utc(2026, 3, 1), periodEnd = Utc(2026, 3, 28) });

    [Fact]
    public void A_payday_on_the_first_makes_the_period_the_2nd_to_the_1st() =>
        Salary.Scenario("A payday on the 1st makes the period the 2nd to the 1st")
            .Given(new PaydayConfigured(Home, 1))
            .ThenView("Current Period", Current, new { periodStart = Utc(2026, 3, 2), periodEnd = Utc(2026, 4, 1) });

    [Fact]
    public void The_latest_payday_setting_wins() =>
        Salary.Scenario("The latest payday setting wins")
            .Given(new PaydayConfigured(Home, 10), new PaydayConfigured(Home, 20))
            .ThenView("Current Period", Current, new { periodStart = Utc(2026, 2, 21), periodEnd = Utc(2026, 3, 20) });

    // ── Available Periods ────────────────────────────────────────────────────

    static AvailablePeriodsResponse Available(IReadOnlyList<object> h) => AvailablePeriodsView.Project(h, Now);
    static Func<IReadOnlyList<object>, AvailablePeriodsResponse> AvailableAt(DateTime at) => h => AvailablePeriodsView.Project(h, at);

    static HouseholdCreated CreatedOn(string timestamp) => new(Home, "Home", "hash") { TimestampUtc = timestamp };

    static PeriodClosed Closed(Guid id, DateTime start, DateTime end, params PeriodPayout[] payouts) =>
        new(id, Home, start, end, payouts, end.AddDays(1));

    static object Period(DateTime start, DateTime end, bool closed = false, Guid? id = null) =>
        new { periodStart = start, periodEnd = end, isClosed = closed, periodId = id };

    [Fact]
    public void The_period_in_progress_comes_first_then_the_completed_ones_back_to_the_household_start() =>
        Salary.Scenario("The period in progress comes first, then completed ones back to the household's start")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"))
            .ThenView("Available Periods", Available, new
            {
                periods = new[]
                {
                    Period(Utc(2026, 2, 26), Utc(2026, 3, 25)),
                    Period(Utc(2026, 1, 26), Utc(2026, 2, 25)),
                    Period(Utc(2025, 12, 26), Utc(2026, 1, 25)),
                },
            });

    [Fact]
    public void A_closed_period_is_marked_with_its_id() =>
        Salary.Scenario("A closed period is marked with its id")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"), Closed(PeriodOne, Utc(2026, 1, 26), Utc(2026, 2, 25)))
            .ThenView("Available Periods", Available, new
            {
                periods = new[]
                {
                    Period(Utc(2026, 2, 26), Utc(2026, 3, 25)),
                    Period(Utc(2026, 1, 26), Utc(2026, 2, 25), closed: true, id: PeriodOne),
                    Period(Utc(2025, 12, 26), Utc(2026, 1, 25)),
                },
            });

    [Fact]
    public void A_period_closed_with_other_boundaries_does_not_mark_a_period_closed() =>
        Salary.Scenario("A period closed with other boundaries doesn't mark a period closed")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"), Closed(PeriodOne, Utc(2026, 1, 20), Utc(2026, 2, 25)))
            .ThenView("Available Periods", Available, new
            {
                periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25)), Period(Utc(2026, 1, 26), Utc(2026, 2, 25)), Period(Utc(2025, 12, 26), Utc(2026, 1, 25)) },
            });

    [Fact]
    public void When_a_period_was_closed_twice_the_last_closing_is_shown() =>
        Salary.Scenario("When a period was closed twice the last closing is shown")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"), Closed(PeriodOne, Utc(2026, 1, 26), Utc(2026, 2, 25)), Closed(PeriodTwo, Utc(2026, 1, 26), Utc(2026, 2, 25)))
            .ThenView("Available Periods", Available, new
            {
                periods = new[]
                {
                    Period(Utc(2026, 2, 26), Utc(2026, 3, 25)),
                    Period(Utc(2026, 1, 26), Utc(2026, 2, 25), closed: true, id: PeriodTwo),
                    Period(Utc(2025, 12, 26), Utc(2026, 1, 25)),
                },
            });

    [Fact]
    public void With_no_household_start_it_goes_back_24_months() =>
        Salary.Scenario("With no household start it goes back 24 completed periods")
            .Given()
            .ThenView("Available Periods", h => new
            {
                count = Available(h).Periods.Count,
                first = Available(h).Periods[0],
                second = Available(h).Periods[1],
                last = Available(h).Periods[^1],
            }, new
            {
                count = 25,
                first = Period(Utc(2026, 2, 26), Utc(2026, 3, 25)),
                second = Period(Utc(2026, 1, 26), Utc(2026, 2, 25)),
                last = Period(Utc(2024, 2, 26), Utc(2024, 3, 25)),
            });

    [Fact]
    public void A_household_started_this_period_has_only_the_period_in_progress() =>
        Salary.Scenario("A household started this period has only the period in progress")
            .Given(CreatedOn("2026-03-10T12:00:00.0000000Z"))
            .ThenView("Available Periods", Available, new { periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25)) } });

    [Fact]
    public void A_household_started_on_the_day_a_period_ended_still_gets_that_period() =>
        Salary.Scenario("A household started on the day a period ended still gets that period")
            .Given(CreatedOn("2026-02-25T12:00:00.0000000Z"))
            .ThenView("Available Periods", Available, new { periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25)), Period(Utc(2026, 1, 26), Utc(2026, 2, 25)) } });

    [Fact]
    public void A_household_started_the_day_after_a_period_ended_does_not() =>
        Salary.Scenario("A household started the day after a period ended doesn't get it")
            .Given(CreatedOn("2026-02-26T12:00:00.0000000Z"))
            .ThenView("Available Periods", Available, new { periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25)) } });

    [Fact]
    public void An_unreadable_start_time_puts_no_limit_on_how_far_back_it_goes() =>
        Salary.Scenario("An unreadable start time puts no limit on how far back it goes")
            .Given(CreatedOn("not a time"))
            .ThenView("Available Periods", h => new { count = Available(h).Periods.Count }, new { count = 25 });

    [Fact]
    public void On_payday_the_period_ending_today_is_completed_but_not_in_progress() =>
        Salary.Scenario("On payday the period ending today is listed once, as completed")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"))
            .ThenView("Available Periods", AvailableAt(new DateTime(2026, 3, 25, 9, 0, 0, DateTimeKind.Utc)), new
            {
                periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25)), Period(Utc(2026, 1, 26), Utc(2026, 2, 25)), Period(Utc(2025, 12, 26), Utc(2026, 1, 25)) },
            });

    [Fact]
    public void On_payday_the_closed_period_ending_today_is_marked_closed() =>
        Salary.Scenario("On payday the period ending today can already be closed")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"), Closed(PeriodOne, Utc(2026, 2, 26), Utc(2026, 3, 25)))
            .ThenView("Available Periods", AvailableAt(new DateTime(2026, 3, 25, 9, 0, 0, DateTimeKind.Utc)), new
            {
                periods = new[] { Period(Utc(2026, 2, 26), Utc(2026, 3, 25), closed: true, id: PeriodOne), Period(Utc(2026, 1, 26), Utc(2026, 2, 25)), Period(Utc(2025, 12, 26), Utc(2026, 1, 25)) },
            });

    [Fact]
    public void The_day_after_payday_a_new_period_is_in_progress_and_the_last_one_is_completed() =>
        Salary.Scenario("The day after payday a new period is in progress and the last one is completed")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"))
            .ThenView("Available Periods", AvailableAt(new DateTime(2026, 3, 26, 9, 0, 0, DateTimeKind.Utc)), new
            {
                periods = new[]
                {
                    Period(Utc(2026, 3, 26), Utc(2026, 4, 25)),
                    Period(Utc(2026, 2, 26), Utc(2026, 3, 25)),
                    Period(Utc(2026, 1, 26), Utc(2026, 2, 25)),
                    Period(Utc(2025, 12, 26), Utc(2026, 1, 25)),
                },
            });

    [Fact]
    public void The_configured_payday_moves_the_listed_periods() =>
        Salary.Scenario("The configured payday moves the listed periods")
            .Given(CreatedOn("2026-01-10T12:00:00.0000000Z"), new PaydayConfigured(Home, 10))
            .ThenView("Available Periods", Available, new
            {
                periods = new[]
                {
                    Period(Utc(2026, 3, 11), Utc(2026, 4, 10)),
                    Period(Utc(2026, 2, 11), Utc(2026, 3, 10)),
                    Period(Utc(2026, 1, 11), Utc(2026, 2, 10)),
                    Period(Utc(2025, 12, 11), Utc(2026, 1, 10)),
                },
            });

    // ── Payout History ───────────────────────────────────────────────────────

    static PayoutHistoryResponse History(IReadOnlyList<object> h) => PayoutHistoryView.Project(h);

    static PeriodPayout PayoutOf(Guid member, string name, decimal baseSalary, decimal deductions, decimal bonuses, decimal net,
        PayoutDeduction[]? deductionLines = null, PayoutBonus[]? bonusLines = null) =>
        new(member, name, baseSalary, deductions, bonuses, net, deductionLines ?? [], bonusLines ?? []);

    [Fact]
    public void The_payout_history_is_empty_before_any_period_is_closed() =>
        Salary.Scenario("The payout history is empty before any period is closed")
            .Given(AnnJoined, SalaryOf(Ann, 1000m))
            .ThenView("Payout History", History, new { periods = Array.Empty<object>() });

    [Fact]
    public void The_payout_history_lists_closed_periods_newest_first_with_each_payout() =>
        Salary.Scenario("The payout history lists closed periods, newest first, with each payout")
            .Given(
                Closed(PeriodOne, Utc(2026, 1, 26), Utc(2026, 2, 25), PayoutOf(Ann, "Ann", 1000m, 20m, 0m, 980m)),
                Closed(PeriodTwo, Utc(2026, 2, 26), Utc(2026, 3, 25), PayoutOf(Ann, "Ann", 1000m, 0m, 15m, 1015m), PayoutOf(Bob, "Bob", 500m, 10m, 0m, 490m)))
            .ThenView("Payout History", History, new
            {
                periods = new object[]
                {
                    new
                    {
                        periodId = PeriodTwo, periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25),
                        payouts = new[]
                        {
                            new { memberId = Ann, name = "Ann", baseSalary = 1000m, deductions = 0m, bonuses = 15m, netPay = 1015m },
                            new { memberId = Bob, name = "Bob", baseSalary = 500m, deductions = 10m, bonuses = 0m, netPay = 490m },
                        },
                    },
                    new
                    {
                        periodId = PeriodOne, periodStart = Utc(2026, 1, 26), periodEnd = Utc(2026, 2, 25),
                        payouts = new[] { new { memberId = Ann, name = "Ann", baseSalary = 1000m, deductions = 20m, bonuses = 0m, netPay = 980m } },
                    },
                },
            });

    [Fact]
    public void A_closed_period_stays_listed_under_the_name_it_was_paid_under() =>
        Salary.Scenario("A closed period stays listed under the name it was paid under")
            .Given(Closed(PeriodOne, Utc(2026, 1, 26), Utc(2026, 2, 25), PayoutOf(Ann, "Ann", 1000m, 0m, 0m, 1000m)), AnnJoined, new MemberNicknameChanged(Ann, Home, "Ann", "Annie"))
            .ThenView("Payout History", History, new { periods = new[] { new { periodId = PeriodOne, payouts = new[] { new { name = "Ann" } } } } });

    // ── Official Salary Slip ─────────────────────────────────────────────────

    static Func<IReadOnlyList<object>, OfficialSalarySlipResponse?> Slip(Guid period, Guid member) =>
        h => OfficialSalarySlipView.Project(h, period, member);

    static readonly PeriodClosed Paid = Closed(PeriodOne, Utc(2026, 2, 26), Utc(2026, 3, 25),
        PayoutOf(Ann, "Ann", 1000m, 25m, 10m, 985m,
            [new PayoutDeduction(DishesId, "Dishes", 10m, 1.5m, 15m), new PayoutDeduction(DishesId, "Dishes", 10m, 1m, 10m)],
            [new PayoutBonus(LaundryId, "Laundry", 5m, 2m, 10m)]),
        PayoutOf(Bob, "Bob", 500m, 0m, 0m, 500m));

    [Fact]
    public void A_salary_slip_shows_the_payout_line_by_line() =>
        Salary.Scenario("A salary slip shows the payout line by line")
            .Given(Paid)
            .ThenView("Official Salary Slip", Slip(PeriodOne, Ann), new
            {
                periodId = PeriodOne, periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25),
                memberName = "Ann", baseSalary = 1000m,
                deductions = new[]
                {
                    new { choreName = "Dishes", baseRate = 10m, multiplier = 1.5m, amount = 15m },
                    new { choreName = "Dishes", baseRate = 10m, multiplier = 1m, amount = 10m },
                },
                bonuses = new[] { new { choreName = "Laundry", baseRate = 5m, multiplier = 2m, amount = 10m } },
                grossDeductions = 25m, grossBonuses = 10m, netPay = 985m,
            });

    [Fact]
    public void A_slip_for_a_member_with_nothing_to_adjust_has_no_lines() =>
        Salary.Scenario("A slip for a member with nothing to adjust has no lines")
            .Given(Paid)
            .ThenView("Official Salary Slip", Slip(PeriodOne, Bob), new
            {
                memberName = "Bob", baseSalary = 500m, deductions = Array.Empty<object>(), bonuses = Array.Empty<object>(), netPay = 500m,
            });

    [Fact]
    public void The_slip_comes_from_the_period_asked_for() =>
        Salary.Scenario("The slip comes from the period asked for")
            .Given(Paid, Closed(PeriodTwo, Utc(2026, 3, 26), Utc(2026, 4, 25), PayoutOf(Ann, "Ann", 1100m, 0m, 0m, 1100m)))
            .ThenView("Official Salary Slip", Slip(PeriodTwo, Ann), new { periodId = PeriodTwo, baseSalary = 1100m, netPay = 1100m });

    [Fact]
    public void There_is_no_slip_for_a_period_that_was_not_closed() =>
        Salary.Scenario("There is no slip for a period that wasn't closed")
            .Given(Paid)
            .ThenView("Official Salary Slip", Slip(PeriodTwo, Ann), (object?)null);

    [Fact]
    public void There_is_no_slip_for_a_member_who_was_not_paid_in_that_period() =>
        Salary.Scenario("There is no slip for a member who wasn't paid in that period")
            .Given(Paid)
            .ThenView("Official Salary Slip", Slip(PeriodOne, Guid.Parse("a0000000-0000-0000-0000-0000000000ff")), (object?)null);
}
