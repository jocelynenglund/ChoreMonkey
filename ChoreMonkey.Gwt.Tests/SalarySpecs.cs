using ChoreMonkey.Core.Feature.Salary.Commands.ClosePeriod;
using ChoreMonkey.Core.Feature.Salary.Commands.SetChoreRates;
using ChoreMonkey.Core.Feature.Salary.Commands.SetMemberSalary;
using ChoreMonkey.Core.Feature.Salary.Commands.SetPayday;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Salary chapter, commands: one test per scenario, the happy case first, then each refusal or branch.
/// Pay periods run from the day after a payday up to and including the next payday (payday 25 unless set).
/// </summary>
public class SalarySpecs
{
    static readonly HistoryChapter Salary = new("Salary");

    static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid PauseA = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    static readonly Guid ClosedId = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    // Thursday 26 March 2026, the day after payday: the period 26 Feb - 25 Mar has just ended.
    static readonly DateTime Now = new(2026, 3, 26, 10, 0, 0, DateTimeKind.Utc);

    static readonly HouseholdCreated Household = new(Home, "Home", "hash");
    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.NewGuid(), "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");
    static MemberSalarySet SalaryOf(Guid member, decimal baseSalary, decimal deduction = 1m, decimal bonus = 1m) =>
        new(Home, member, baseSalary, deduction, bonus, Now);
    static ChoreCreated Daily(Guid id, string name, DateTime? start = null, decimal missed = 10m) =>
        new(id, Home, name, "", new ChoreFrequency("daily"), StartDate: start, MissedDeduction: missed);
    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);
    static ChoreRatesSet Rates(Guid chore, decimal? deduction, decimal? bonus) => new(Home, chore, deduction, bonus, Now);

    // ── Set Member Salary ────────────────────────────────────────────────────

    static Decision SetSalary(IReadOnlyList<object> h, SetMemberSalaryCommand c) => SetMemberSalarySlice.Decide(h, c, Now);

    [Fact]
    public void A_members_salary_is_set() =>
        Salary.Scenario("A member's salary is set")
            .Given()
            .When("Set Member Salary", new SetMemberSalaryCommand(Home, Ann, 1000m, 1.5m, 2m), SetSalary)
            .Then(Event<MemberSalarySet>(new
            {
                householdId = Home, memberId = Ann, baseSalary = 1000m, deductionMultiplier = 1.5m, bonusMultiplier = 2m, setAt = Now,
            }));

    [Fact]
    public void Setting_the_salary_again_records_it_again() =>
        Salary.Scenario("Setting the salary again records it again")
            .Given(SalaryOf(Ann, 1000m))
            .When("Set Member Salary", new SetMemberSalaryCommand(Home, Ann, 1200m, 1m, 1m), SetSalary)
            .Then(Event<MemberSalarySet>(new { memberId = Ann, baseSalary = 1200m }));

    [Fact]
    public void A_salary_can_be_set_for_someone_who_is_not_a_member() =>
        Salary.Scenario("A salary can be set for someone who is not a member")
            .Given()
            .When("Set Member Salary", new SetMemberSalaryCommand(Home, Bob, 0m, 0m, 0m), SetSalary)
            .Then(Event<MemberSalarySet>(new { memberId = Bob, baseSalary = 0m, deductionMultiplier = 0m, bonusMultiplier = 0m }));

    // ── Set Chore Rates ──────────────────────────────────────────────────────

    static Decision SetRates(IReadOnlyList<object> h, SetChoreRatesCommand c) => SetChoreRatesSlice.Decide(h, c, Now);

    [Fact]
    public void A_chores_rates_are_set() =>
        Salary.Scenario("A chore's rates are set")
            .Given()
            .When("Set Chore Rates", new SetChoreRatesCommand(Home, DishesId, 15m, 5m), SetRates)
            .Then(Event<ChoreRatesSet>(new { householdId = Home, choreId = DishesId, deductionRate = 15m, bonusRate = 5m, setAt = Now }));

    [Fact]
    public void Leaving_a_rate_out_clears_it() =>
        Salary.Scenario("Leaving a rate out clears it back to the chore's own default")
            .Given(Rates(DishesId, 15m, 5m))
            .When("Set Chore Rates", new SetChoreRatesCommand(Home, DishesId, null, 5m), SetRates)
            .Then(Event<ChoreRatesSet>(new { choreId = DishesId, deductionRate = (decimal?)null, bonusRate = 5m }));

    // ── Set Payday ───────────────────────────────────────────────────────────

    [Fact]
    public void The_payday_is_set() =>
        Salary.Scenario("The payday is set")
            .Given()
            .When("Set Payday", new SetPaydayCommand(Home, 15), SetPaydaySlice.Decide)
            .Then(Event<PaydayConfigured>(new { householdId = Home, paydayDayOfMonth = 15 }));

    [Fact]
    public void The_first_of_the_month_is_a_payday() =>
        Salary.Scenario("The first of the month is a valid payday")
            .Given()
            .When("Set Payday", new SetPaydayCommand(Home, 1), SetPaydaySlice.Decide)
            .Then(Event<PaydayConfigured>(new { paydayDayOfMonth = 1 }));

    [Fact]
    public void The_28th_is_the_latest_payday() =>
        Salary.Scenario("The 28th is the latest valid payday")
            .Given()
            .When("Set Payday", new SetPaydayCommand(Home, 28), SetPaydaySlice.Decide)
            .Then(Event<PaydayConfigured>(new { paydayDayOfMonth = 28 }));

    [Fact]
    public void Changing_the_payday_records_it_again() =>
        Salary.Scenario("Changing the payday records it again")
            .Given(new PaydayConfigured(Home, 15))
            .When("Set Payday", new SetPaydayCommand(Home, 20), SetPaydaySlice.Decide)
            .Then(Event<PaydayConfigured>(new { paydayDayOfMonth = 20 }));

    [Fact]
    public void Day_zero_is_not_a_payday() =>
        Salary.Scenario("Day 0 is not a payday")
            .Given()
            .When("Set Payday", new SetPaydayCommand(Home, 0), SetPaydaySlice.Decide)
            .ThenNothing("Payday must be between 1 and 28.", "Invalid");

    [Fact]
    public void The_29th_is_not_a_payday() =>
        Salary.Scenario("The 29th is not a payday")
            .Given()
            .When("Set Payday", new SetPaydayCommand(Home, 29), SetPaydaySlice.Decide)
            .ThenNothing("Payday must be between 1 and 28.", "Invalid");

    // ── Close Period ─────────────────────────────────────────────────────────

    static ClosePeriodCommand Close(DateTime? target = null) => new(Home, target);
    static Decision ClosePeriod(IReadOnlyList<object> h, ClosePeriodCommand c) => ClosePeriodSlice.Decide(h, c, Now);
    static Decision ClosePeriodAt(DateTime now, IReadOnlyList<object> h, ClosePeriodCommand c) => ClosePeriodSlice.Decide(h, c, now);

    static PeriodClosed Closed(DateTime start, DateTime end) =>
        new(ClosedId, Home, start, end, [], Now);

    static readonly object AnyId = Where(j => Guid.TryParse(j.GetString(), out var g) && g != Guid.Empty);

    [Fact]
    public void The_last_completed_period_is_closed() =>
        Salary.Scenario("The last completed period is closed")
            .Given()
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                periodId = AnyId, householdId = Home,
                periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25),
                payouts = Array.Empty<object>(), closedAt = Now,
            }));

    [Fact]
    public void A_member_is_paid_their_base_salary() =>
        Salary.Scenario("A member is paid their base salary")
            .Given(AnnJoined, SalaryOf(Ann, 1000m))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[]
                {
                    new
                    {
                        memberId = Ann, name = "Ann", baseSalary = 1000m, grossDeductions = 0m, grossBonuses = 0m, netPay = 1000m,
                        deductions = Array.Empty<object>(), bonuses = Array.Empty<object>(),
                    },
                },
            }));

    [Fact]
    public void A_member_with_no_salary_set_is_paid_nothing() =>
        Salary.Scenario("A member with no salary set is paid nothing")
            .Given(AnnJoined)
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, baseSalary = 0m, netPay = 0m } } }));

    [Fact]
    public void Every_active_member_gets_a_payout() =>
        Salary.Scenario("Every active member gets a payout")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 1000m), SalaryOf(Bob, 500m))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[]
                {
                    new { memberId = Ann, name = "Ann", netPay = 1000m },
                    new { memberId = Bob, name = "Bob", netPay = 500m },
                },
            }));

    [Fact]
    public void A_renamed_member_is_paid_under_the_new_name() =>
        Salary.Scenario("A renamed member is paid under the new name")
            .Given(AnnJoined, new MemberNicknameChanged(Ann, Home, "Ann", "Annie"))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, name = "Annie" } } }));

    [Fact]
    public void A_removed_member_is_not_paid() =>
        Salary.Scenario("A removed member is not paid")
            .Given(AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann } } }));

    [Fact]
    public void Each_missed_day_of_a_daily_chore_is_deducted() =>
        Salary.Scenario("Each missed day of a daily chore is deducted")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 24)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[]
                {
                    new
                    {
                        memberId = Ann, baseSalary = 1000m, grossDeductions = 20m, grossBonuses = 0m, netPay = 980m,
                        deductions = new[]
                        {
                            new { choreId = DishesId, choreName = "Dishes", baseRate = 10m, multiplier = 1m, amount = 10m },
                            new { choreId = DishesId, choreName = "Dishes", baseRate = 10m, multiplier = 1m, amount = 10m },
                        },
                    },
                },
            }));

    [Fact]
    public void The_whole_period_counts_with_no_grace_days() =>
        Salary.Scenario("The whole period counts, with no grace days: payday itself can be missed")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 10m, netPay = 990m } } }));

    [Fact]
    public void A_chore_starting_after_the_period_ends_costs_nothing() =>
        Salary.Scenario("A chore starting after the period ends costs nothing")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 26)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 0m, netPay = 1000m } } }));

    [Fact]
    public void A_completed_day_is_not_deducted() =>
        Salary.Scenario("A completed day is not deducted")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 24)), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 24, 18, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 10m, netPay = 990m } } }));

    [Fact]
    public void Someone_elses_completion_does_not_count() =>
        Salary.Scenario("Someone else's completion doesn't count for Ann")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)), AssignedToAll(DishesId),
                Done(DishesId, Bob, new DateTime(2026, 3, 25, 9, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[] { new { memberId = Ann, grossDeductions = 10m }, new { memberId = Bob, grossDeductions = 0m } },
            }));

    [Fact]
    public void The_deduction_multiplier_scales_the_deduction() =>
        Salary.Scenario("The member's deduction multiplier scales each deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m, deduction: 1.5m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 24)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[]
                {
                    new
                    {
                        grossDeductions = 30m, netPay = 970m,
                        deductions = new[]
                        {
                            new { baseRate = 10m, multiplier = 1.5m, amount = 15m },
                            new { baseRate = 10m, multiplier = 1.5m, amount = 15m },
                        },
                    },
                },
            }));

    [Fact]
    public void A_chore_rate_overrides_the_choresown_deduction() =>
        Salary.Scenario("A chore rate overrides the chore's own deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)), AssignedTo(DishesId, Ann), Rates(DishesId, 4m, null))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 4m, netPay = 996m, deductions = new[] { new { baseRate = 4m, amount = 4m } } } } }));

    [Fact]
    public void A_cleared_chore_rate_falls_back_to_the_choresown_deduction() =>
        Salary.Scenario("A cleared chore rate falls back to the chore's own deduction")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25), missed: 7m), AssignedTo(DishesId, Ann),
                Rates(DishesId, 4m, null), Rates(DishesId, null, null))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 7m, netPay = 993m } } }));

    [Fact]
    public void Pay_never_goes_below_zero() =>
        Salary.Scenario("Pay never goes below zero")
            .Given(AnnJoined, SalaryOf(Ann, 20m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 20)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { baseSalary = 20m, grossDeductions = 60m, netPay = 0m } } }));

    [Fact]
    public void A_chore_not_assigned_to_the_member_is_ignored() =>
        Salary.Scenario("A chore not assigned to the member is ignored")
            .Given(AnnJoined, BobJoined, SalaryOf(Ann, 1000m), SalaryOf(Bob, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)), AssignedTo(DishesId, Bob))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 0m }, new { memberId = Bob, grossDeductions = 10m } } }));

    [Fact]
    public void A_chore_with_no_assignment_is_ignored() =>
        Salary.Scenario("A chore with no assignment is ignored")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 0m, netPay = 1000m } } }));

    [Fact]
    public void A_deleted_chore_is_ignored() =>
        Salary.Scenario("A deleted chore is ignored")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 25)), AssignedTo(DishesId, Ann), new ChoreDeleted(DishesId, Home, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { memberId = Ann, grossDeductions = 0m } } }));

    [Fact]
    public void An_updated_chore_is_judged_as_updated() =>
        Salary.Scenario("An updated chore is judged as updated")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 20)), AssignedTo(DishesId, Ann),
                new ChoreUpdated(DishesId, Home, "Dishes", "", new ChoreFrequency("daily"), false, new DateTime(2026, 3, 25), true, 3m))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 3m, netPay = 997m } } }));

    [Fact]
    public void A_one_time_chore_never_costs_anything() =>
        Salary.Scenario("A one-time chore never costs anything")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("once")), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 0m, netPay = 1000m } } }));

    [Fact]
    public void A_weekly_chore_on_set_days_is_deducted_for_each_missed_day() =>
        Salary.Scenario("A weekly chore on set days is deducted for each missed day")
            // Mondays in the period: 2, 9, 16 and 23 March. The 9th is done.
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly", ["monday"])), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 9, 8, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 30m, netPay = 970m } } }));

    [Fact]
    public void A_weekly_chore_with_no_days_is_deducted_for_each_week_without_a_completion() =>
        Salary.Scenario("A weekly chore with no days is deducted for each week without a completion")
            // Weeks (Monday first) touching the period: 23 Feb, 2, 9, 16 and 23 March. The week of the 9th is done.
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("weekly")), AssignedTo(DishesId, Ann),
                Done(DishesId, Ann, new DateTime(2026, 3, 11, 8, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 40m, netPay = 960m } } }));

    [Fact]
    public void An_interval_chore_is_deducted_each_time_it_falls_due_undone() =>
        Salary.Scenario("An interval chore is deducted each time it falls due undone")
            // Starts 20 March, so it is first due 22 March (every 3 days), then 25 March.
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(DishesId, Home, "Dishes", "", new ChoreFrequency("interval", IntervalDays: 3), StartDate: new DateTime(2026, 3, 20)), AssignedTo(DishesId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 20m, netPay = 980m } } }));

    [Fact]
    public void A_pause_exempts_the_days_it_covers() =>
        Salary.Scenario("A pause exempts the days it covers")
            // Missed would be 20 to 25 March; the pause covers 21 to 23 March, both ends included.
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 20)), AssignedTo(DishesId, Ann),
                new ChorePaused(PauseA, DishesId, Home, new DateTime(2026, 3, 21), new DateTime(2026, 3, 23), "Holiday"))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 30m, netPay = 970m } } }));

    [Fact]
    public void A_removed_pause_exempts_nothing() =>
        Salary.Scenario("A removed pause exempts nothing")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 20)), AssignedTo(DishesId, Ann),
                new ChorePaused(PauseA, DishesId, Home, new DateTime(2026, 3, 21), new DateTime(2026, 3, 23), "Holiday"),
                new ChorePauseRemoved(PauseA, DishesId, Home))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 60m, netPay = 940m } } }));

    [Fact]
    public void A_pause_on_another_chore_exempts_nothing() =>
        Salary.Scenario("A pause on another chore exempts nothing")
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 24)), AssignedTo(DishesId, Ann),
                new ChorePaused(PauseA, LaundryId, Home, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), null))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 20m } } }));

    [Fact]
    public void A_pause_spanning_the_period_start_exempts_only_the_days_inside() =>
        Salary.Scenario("A pause that began before the period exempts the days inside it")
            // Daily from the period start (26 Feb): 28 days. The pause covers 20 Feb to 15 Mar, so 18 days are exempt (26 Feb - 15 Mar).
            .Given(AnnJoined, SalaryOf(Ann, 1000m), Daily(DishesId, "Dishes", missed: 1m), AssignedTo(DishesId, Ann),
                new ChorePaused(PauseA, DishesId, Home, new DateTime(2026, 2, 20), new DateTime(2026, 3, 15), null))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 10m, netPay = 990m } } }));

    [Fact]
    public void An_optional_chore_done_earns_its_bonus() =>
        Salary.Scenario("An optional chore done earns its bonus")
            .Given(AnnJoined, SalaryOf(Ann, 1000m, bonus: 2m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 5m),
                Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)), Done(LaundryId, Ann, new DateTime(2026, 3, 12, 9, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[]
                {
                    new
                    {
                        grossDeductions = 0m, grossBonuses = 20m, netPay = 1020m,
                        bonuses = new[]
                        {
                            new { choreId = LaundryId, choreName = "Laundry", baseRate = 5m, multiplier = 2m, amount = 10m },
                            new { choreId = LaundryId, choreName = "Laundry", baseRate = 5m, multiplier = 2m, amount = 10m },
                        },
                    },
                },
            }));

    [Fact]
    public void An_optional_chore_never_costs_when_missed() =>
        Salary.Scenario("An optional chore costs nothing when it isn't done")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossDeductions = 0m, grossBonuses = 0m, netPay = 1000m } } }));

    [Fact]
    public void An_optional_chore_with_no_bonus_rate_pays_zero() =>
        Salary.Scenario("An optional chore with no bonus rate pays zero")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                payouts = new[] { new { grossBonuses = 0m, netPay = 1000m, bonuses = new[] { new { choreId = LaundryId, baseRate = 0m, amount = 0m } } } },
            }));

    [Fact]
    public void A_bonus_done_on_the_last_day_of_the_period_counts() =>
        Salary.Scenario("A bonus done at the end of the last day of the period counts")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 5m), Done(LaundryId, Ann, new DateTime(2026, 3, 25, 23, 59, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossBonuses = 5m, netPay = 1005m } } }));

    [Fact]
    public void A_bonus_done_the_day_after_the_period_does_not_count() =>
        Salary.Scenario("A bonus done the day after the period doesn't count")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 5m), Done(LaundryId, Ann, new DateTime(2026, 3, 26, 0, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossBonuses = 0m, netPay = 1000m } } }));

    [Fact]
    public void A_bonus_done_at_midnight_opening_the_period_counts() =>
        Salary.Scenario("A bonus done at midnight opening the period counts")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 5m), Done(LaundryId, Ann, new DateTime(2026, 2, 26, 0, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossBonuses = 5m, netPay = 1005m } } }));

    [Fact]
    public void A_bonus_done_just_before_the_period_does_not_count() =>
        Salary.Scenario("A bonus done just before the period doesn't count")
            .Given(AnnJoined, SalaryOf(Ann, 1000m),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 5m), Done(LaundryId, Ann, new DateTime(2026, 2, 25, 23, 59, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { grossBonuses = 0m, netPay = 1000m } } }));

    [Fact]
    public void Deductions_and_bonuses_net_off_together() =>
        Salary.Scenario("Deductions and bonuses net off together")
            .Given(AnnJoined, SalaryOf(Ann, 100m, deduction: 0.5m, bonus: 1.5m),
                Daily(DishesId, "Dishes", start: new DateTime(2026, 3, 24)), AssignedTo(DishesId, Ann),
                new ChoreCreated(LaundryId, Home, "Laundry", "", new ChoreFrequency("daily"), IsOptional: true, IsRequired: false), AssignedTo(LaundryId, Ann),
                Rates(LaundryId, null, 4m), Done(LaundryId, Ann, new DateTime(2026, 3, 10, 9, 0, 0)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { payouts = new[] { new { baseSalary = 100m, grossDeductions = 10m, grossBonuses = 6m, netPay = 96m } } }));

    [Fact]
    public void A_chosen_period_end_closes_that_period() =>
        Salary.Scenario("A chosen period end closes that period")
            .Given(AnnJoined, SalaryOf(Ann, 1000m))
            .When("Close Period", Close(new DateTime(2026, 2, 25)), ClosePeriod)
            .Then(Event<PeriodClosed>(new
            {
                periodStart = Utc(2026, 1, 26), periodEnd = new DateTime(2026, 2, 25), // kept as asked for (no UTC kind)
                payouts = new[] { new { memberId = Ann, netPay = 1000m } },
            }));

    [Fact]
    public void The_chosen_period_end_ignores_the_time_of_day() =>
        Salary.Scenario("The chosen period end ignores the time of day")
            .Given()
            .When("Close Period", Close(new DateTime(2026, 2, 25, 22, 30, 0)), ClosePeriod)
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 1, 26), periodEnd = new DateTime(2026, 2, 25) }));

    [Fact]
    public void The_configured_payday_sets_the_period() =>
        Salary.Scenario("The configured payday sets the period")
            .Given(new PaydayConfigured(Home, 10))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 2, 11), periodEnd = Utc(2026, 3, 10) }));

    [Fact]
    public void The_latest_payday_setting_wins() =>
        Salary.Scenario("The latest payday setting wins")
            .Given(new PaydayConfigured(Home, 10), new PaydayConfigured(Home, 20))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 2, 21), periodEnd = Utc(2026, 3, 20) }));

    [Fact]
    public void Before_payday_the_last_completed_period_is_the_one_before() =>
        Salary.Scenario("Before payday, the last completed period is the one before")
            .Given()
            .When("Close Period", Close(), (h, c) => ClosePeriodAt(new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc), h, c))
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 1, 26), periodEnd = Utc(2026, 2, 25) }));

    [Fact]
    public void On_payday_itself_the_period_can_be_closed() =>
        Salary.Scenario("On payday itself the period ending that day can be closed")
            .Given()
            .When("Close Period", Close(), (h, c) => ClosePeriodAt(new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc), h, c))
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25) }));

    [Fact]
    public void A_period_still_running_cant_be_closed() =>
        Salary.Scenario("A period still running can't be closed")
            .Given()
            .When("Close Period", Close(new DateTime(2026, 3, 25)), (h, c) => ClosePeriodAt(new DateTime(2026, 3, 24, 23, 59, 0, DateTimeKind.Utc), h, c))
            .ThenNothing("Period has not ended yet. It closes on 2026-03-25.", "Invalid");

    [Fact]
    public void A_period_cant_be_closed_twice() =>
        Salary.Scenario("A period can't be closed twice")
            .Given(Closed(new DateTime(2026, 2, 26), new DateTime(2026, 3, 25)))
            .When("Close Period", Close(), ClosePeriod)
            .ThenNothing("This period has already been closed.", "Conflict");

    [Fact]
    public void A_period_chosen_by_its_end_cant_be_closed_twice() =>
        Salary.Scenario("A period chosen by its end can't be closed twice, whatever the time of day")
            .Given(Closed(new DateTime(2026, 1, 26), new DateTime(2026, 2, 25, 8, 0, 0)))
            .When("Close Period", Close(new DateTime(2026, 2, 25)), ClosePeriod)
            .ThenNothing("This period has already been closed.", "Conflict");

    [Fact]
    public void Closing_another_period_is_fine_when_one_is_closed() =>
        Salary.Scenario("Closing another period is fine when a different one is closed")
            .Given(Closed(new DateTime(2026, 1, 26), new DateTime(2026, 2, 25)))
            .When("Close Period", Close(), ClosePeriod)
            .Then(Event<PeriodClosed>(new { periodStart = Utc(2026, 2, 26), periodEnd = Utc(2026, 3, 25) }));
}
