using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Salary.Commands.ClosePeriod;

/// <summary>What closing a period came to: the decision, and (when accepted) the response the caller gets back.</summary>
public record ClosePeriodOutcome(Decision Decision, ClosePeriodResponse? Response);

/// <summary>
/// Close Period: works out the pay period (the one asked for, or the last completed one), refuses a period that has
/// not ended yet or is already closed, then records PeriodClosed with every active member's payout: base salary less
/// the missed required chores (no grace period) plus the optional chores done, never below zero.
/// History is the household's events, the salary events and the chores' events together.
/// </summary>
public static class ClosePeriodSlice
{
    public const string NotEnded = "Period has not ended yet. It closes on {0:yyyy-MM-dd}.";
    public const string AlreadyClosed = "This period has already been closed.";

    public static Decision Decide(IReadOnlyList<object> history, ClosePeriodCommand cmd, DateTime now) =>
        Close(history, cmd, now).Decision;

    /// <summary>Same as <see cref="Decide"/>, but also hands back the response (it carries detail the event does not).</summary>
    public static ClosePeriodOutcome Close(IReadOnlyList<object> history, ClosePeriodCommand cmd, DateTime now)
    {
        var today = now.Date;

        // Determine payday from configuration, default to 25
        var paydayDay = SalaryFolds.Payday(history);

        // If a specific period end was requested, use it; otherwise default to most recent completed period
        DateTime periodStart, periodEnd;
        if (cmd.TargetPeriodEnd.HasValue)
        {
            periodEnd = cmd.TargetPeriodEnd.Value.Date;
            var prevPayday = new DateTime(periodEnd.Year, periodEnd.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            periodStart = new DateTime(prevPayday.Year, prevPayday.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        }
        else
        {
            (periodStart, periodEnd) = GetLastCompletedPayPeriod(today, paydayDay);
        }

        // Guard: refuse to close a period that hasn't ended yet
        if (today < periodEnd)
            return new(Decision.Invalid(string.Format(NotEnded, periodEnd)), null);

        // Guard: already closed?
        var alreadyClosed = history.OfType<PeriodClosed>().Any(e => e.PeriodEnd.Date == periodEnd);
        if (alreadyClosed)
            return new(Decision.Conflict(AlreadyClosed), null);

        var periodId = Guid.NewGuid();

        var activeMembers = SalaryFolds.ActiveMembers(history);
        var salaryConfigs = SalaryFolds.SalaryConfigs(history);
        var choreRates = SalaryFolds.ChoreRates(history);
        var chores = SalaryFolds.LiveChores(history);
        var assignments = SalaryFolds.Assignments(history);

        // Active pause windows per chore — missed instances within a window are exempt.
        var chorePauses = ChorePauses.Build(history);

        // Get completions in period (end-exclusive)
        var completionsInPeriod = history
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt < periodEnd.AddDays(1))
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all completions for last-completed tracking
        var allCompletions = SalaryFolds.LastCompletions(history);

        // Calculate payouts
        var payouts = new List<PeriodPayout>();
        var payoutDtos = new List<PayoutSummaryDto>();

        foreach (var memberId in activeMembers.Keys)
        {
            var config = salaryConfigs.GetValueOrDefault(memberId);
            var baseSalary = config?.BaseSalary ?? 0m;
            var deductionMultiplier = config?.DeductionMultiplier ?? 1.0m;
            var bonusMultiplier = config?.BonusMultiplier ?? 1.0m;

            var deductions = new List<PayoutDeduction>();
            var bonuses = new List<PayoutBonus>();
            var missedChoresDto = new List<MissedChoreDto>();
            var bonusChoresDto = new List<BonusChoreDto>();
            decimal totalDeductions = 0m;
            decimal totalBonuses = 0m;

            foreach (var (choreId, chore) in chores)
            {
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
                if (!isAssigned) continue;

                var rates = choreRates.GetValueOrDefault(choreId);
                var lastCompletion = allCompletions.GetValueOrDefault((choreId, memberId));

                if (chore.IsOptional)
                {
                    // Bonus chores
                    var completions = completionsInPeriod.GetValueOrDefault((choreId, memberId));
                    if (completions != null)
                    {
                        var bonusRate = rates?.BonusRate ?? 0m;
                        foreach (var completion in completions)
                        {
                            var amount = bonusRate * bonusMultiplier;
                            bonuses.Add(new PayoutBonus(choreId, chore.DisplayName, bonusRate, bonusMultiplier, amount));
                            bonusChoresDto.Add(new BonusChoreDto(choreId, chore.DisplayName, completion.CompletedAt, amount));
                            totalBonuses += amount;
                        }
                    }
                }
                else
                {
                    // Required chores - calculate missed for entire period (no grace period when closing)
                    var missed = CalculateMissedInstances(
                        chore,
                        lastCompletion?.CompletedAt,
                        periodStart,
                        periodEnd,
                        completionsInPeriod.GetValueOrDefault((choreId, memberId)),
                        chorePauses.GetValueOrDefault(choreId));
                    
                    var deductionRate = rates?.DeductionRate ?? chore.MissedDeduction;
                    foreach (var period in missed)
                    {
                        var amount = deductionRate * deductionMultiplier;
                        deductions.Add(new PayoutDeduction(choreId, chore.DisplayName, deductionRate, deductionMultiplier, amount));
                        missedChoresDto.Add(new MissedChoreDto(choreId, chore.DisplayName, period, amount));
                        totalDeductions += amount;
                    }
                }
            }

            var netPay = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);
            var memberName = activeMembers.GetValueOrDefault(memberId) ?? "Unknown";

            payouts.Add(new PeriodPayout(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay,
                deductions.AsReadOnly(),
                bonuses.AsReadOnly()));

            payoutDtos.Add(new PayoutSummaryDto(
                memberId,
                memberName,
                baseSalary,
                totalDeductions,
                totalBonuses,
                netPay,
                missedChoresDto,
                bonusChoresDto));
        }

        // The period closed event
        var closedEvent = new PeriodClosed(
            periodId,
            cmd.HouseholdId,
            periodStart,
            periodEnd,
            payouts.AsReadOnly(),
            now);

        return new(Decision.Accept(closedEvent), new ClosePeriodResponse(periodId, periodStart, periodEnd, payoutDtos));
    }

    private static (DateTime start, DateTime end) GetLastCompletedPayPeriod(DateTime today, int paydayDay)
    {
        var paydayThisMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        if (today >= paydayThisMonth.Date)
        {
            // We're past payday this month — the period that just ended is: (payday last month + 1 day) → payday this month
            var prevMonth = paydayThisMonth.AddMonths(-1);
            var start = new DateTime(prevMonth.Year, prevMonth.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            return (start, paydayThisMonth);
        }
        else
        {
            // Before payday this month — last completed period ended on payday last month
            var paydayLastMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            var twoMonthsAgo = paydayLastMonth.AddMonths(-1);
            var start = new DateTime(twoMonthsAgo.Year, twoMonthsAgo.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            return (start, paydayLastMonth);
        }
    }

    private static List<string> CalculateMissedInstances(
        ChoreCreated chore,
        DateTime? lastCompleted,
        DateTime periodStart,
        DateTime periodEnd,
        List<ChoreCompleted>? completionsInPeriod,
        List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var frequency = chore.Frequency;
        if (frequency == null) return missed;

        var choreStart = chore.StartDate?.Date ?? periodStart;
        var effectiveStart = choreStart > periodStart ? choreStart : periodStart;

        switch (frequency.Type.ToLower())
        {
            case "daily":
                missed = CalculateDailyMissed(effectiveStart, periodEnd, completionsInPeriod, pauses);
                break;
            case "weekly":
                missed = CalculateWeeklyMissed(frequency.Days, effectiveStart, periodEnd, completionsInPeriod, pauses);
                break;
            case "interval":
                missed = CalculateIntervalMissed(frequency.IntervalDays ?? 1, effectiveStart, periodEnd, lastCompleted, completionsInPeriod, pauses);
                break;
        }

        return missed;
    }

    private static List<string> CalculateDailyMissed(DateTime start, DateTime end, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (!completedDates.Contains(date) && !pauses.CoversDate(date))
            {
                missed.Add(date.ToString("yyyy-MM-dd"));
            }
        }
        return missed;
    }

    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static List<string> CalculateWeeklyMissed(string[]? days, DateTime start, DateTime end, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        if (days == null || days.Length == 0)
        {
            var currentWeek = GetMondayOfWeek(start);
            var endWeek = GetMondayOfWeek(end);

            while (currentWeek <= endWeek)
            {
                var weekEnd = currentWeek.AddDays(6);
                var completedThisWeek = completedDates.Any(d => d >= currentWeek && d <= weekEnd);
                // Exempt the week if a pause window overlaps any day of it.
                var pausedThisWeek = pauses.CoversAnyInRange(currentWeek, weekEnd);

                if (!completedThisWeek && !pausedThisWeek)
                {
                    var weekNum = System.Globalization.CultureInfo.InvariantCulture.Calendar
                        .GetWeekOfYear(currentWeek, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    missed.Add($"{currentWeek.Year}-W{weekNum:D2}");
                }
                currentWeek = currentWeek.AddDays(7);
            }
        }
        else
        {
            var requiredDays = days.Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true)).ToHashSet();

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (requiredDays.Contains(date.DayOfWeek) && !completedDates.Contains(date) && !pauses.CoversDate(date))
                {
                    missed.Add(date.ToString("yyyy-MM-dd"));
                }
            }
        }

        return missed;
    }

    private static List<string> CalculateIntervalMissed(int intervalDays, DateTime start, DateTime end, DateTime? lastCompleted, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).OrderBy(d => d).ToList() ?? new List<DateTime>();

        var lastDue = lastCompleted?.Date ?? start.AddDays(-1);
        var idx = 0;

        while (true)
        {
            var nextDue = lastDue.AddDays(intervalDays);
            if (nextDue > end) break;

            while (idx < completedDates.Count && completedDates[idx] <= nextDue)
            {
                lastDue = completedDates[idx];
                idx++;
                nextDue = lastDue.AddDays(intervalDays);
                if (nextDue > end) break;
            }

            if (nextDue > end) break;

            // Advance the schedule past this due date regardless, but only record
            // it as missed when the due date isn't within a pause window.
            if (!pauses.CoversDate(nextDue))
            {
                missed.Add(nextDue.ToString("yyyy-MM-dd"));
            }
            lastDue = nextDue;
        }

        return missed;
    }
}
