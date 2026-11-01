using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Salary.Queries.GetCurrentPeriod;

/// <summary>
/// The CurrentPeriod view: each active member's projected pay for the pay period that contains today, folded from the
/// household, salary and chores events by the slice itself. Missed required chores count only once they are more than
/// two days overdue, and pauses exempt the days (or the week) they cover. The handler is only the store adapter.
/// </summary>
public static class CurrentPeriodView
{
    private const int GracePeriodDays = 2;

    public static CurrentPeriodResponse Project(IReadOnlyList<object> history, Guid? memberKey, DateTime now)
    {
        var today = now.Date;

        // Determine payday — use last PaydayConfigured event, default to 25
        var paydayDay = SalaryFolds.Payday(history);

        var (periodStart, periodEnd) = GetCurrentPayPeriod(today, paydayDay);

        // Active members with current nicknames (handles renames and removals)
        var activeMembers = SalaryFolds.ActiveMembers(history);
        var salaryConfigs = SalaryFolds.SalaryConfigs(history);
        var choreRates = SalaryFolds.ChoreRates(history);
        var chores = SalaryFolds.LiveChores(history);
        var assignments = SalaryFolds.Assignments(history);

        // Active pause windows per chore — missed instances within a window are exempt.
        var chorePauses = ChorePauses.Build(history);

        // Get completions in period grouped by (chore, member)
        var completionsInPeriod = history
            .OfType<ChoreCompleted>()
            .Where(e => e.CompletedAt >= periodStart && e.CompletedAt <= periodEnd)
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all completions for last-completed tracking
        var allCompletions = SalaryFolds.LastCompletions(history);

        // Calculate per-member summaries
        var members = new List<MemberPeriodSummary>();
        var allMemberIds = activeMembers.Keys.ToList();

        if (memberKey.HasValue)
        {
            allMemberIds = allMemberIds.Where(id => id == memberKey.Value).ToList();
        }

        foreach (var memberId in allMemberIds)
        {
            var config = salaryConfigs.GetValueOrDefault(memberId);
            var baseSalary = config?.BaseSalary ?? 0m;
            var deductionMultiplier = config?.DeductionMultiplier ?? 1.0m;
            var bonusMultiplier = config?.BonusMultiplier ?? 1.0m;

            var missedChores = new List<MissedChoreDto>();
            var bonusChores = new List<BonusChoreDto>();
            decimal totalDeductions = 0m;
            decimal totalBonuses = 0m;

            foreach (var (choreId, chore) in chores)
            {
                // Check if assigned to this member
                var assignment = assignments.GetValueOrDefault(choreId);
                var isAssigned = assignment?.AssignToAll == true ||
                    (assignment?.AssignedToMemberIds?.Contains(memberId) ?? false);
                if (!isAssigned) continue;

                var rates = choreRates.GetValueOrDefault(choreId);
                var lastCompletion = allCompletions.GetValueOrDefault((choreId, memberId));

                if (chore.IsOptional)
                {
                    // Bonus chores: count completions in period
                    var completions = completionsInPeriod.GetValueOrDefault((choreId, memberId));
                    if (completions != null)
                    {
                        var bonusRate = rates?.BonusRate ?? 0m;
                        foreach (var completion in completions)
                        {
                            var bonus = bonusRate * bonusMultiplier;
                            bonusChores.Add(new BonusChoreDto(choreId, chore.DisplayName, completion.CompletedAt, bonus));
                            totalBonuses += bonus;
                        }
                    }
                }
                else
                {
                    // Required chores: check for missed instances (>2 days overdue)
                    var missed = CalculateMissedInstances(
                        chore,
                        lastCompletion?.CompletedAt,
                        periodStart,
                        today,
                        completionsInPeriod.GetValueOrDefault((choreId, memberId)),
                        chorePauses.GetValueOrDefault(choreId));
                    
                    var deductionRate = rates?.DeductionRate ?? chore.MissedDeduction;
                    foreach (var period in missed)
                    {
                        var deduction = deductionRate * deductionMultiplier;
                        missedChores.Add(new MissedChoreDto(choreId, chore.DisplayName, period, deduction));
                        totalDeductions += deduction;
                    }
                }
            }

            var projected = Math.Max(0m, baseSalary - totalDeductions + totalBonuses);

            members.Add(new MemberPeriodSummary(
                memberId,
                activeMembers.GetValueOrDefault(memberId) ?? "Unknown",
                baseSalary,
                deductionMultiplier,
                bonusMultiplier,
                totalDeductions,
                totalBonuses,
                projected,
                missedChores,
                bonusChores));
        }

        return new CurrentPeriodResponse(periodStart, periodEnd, members);
    }

    private static (DateTime start, DateTime end) GetCurrentPayPeriod(DateTime today, int paydayDay)
    {
        var paydayThisMonth = new DateTime(today.Year, today.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
        if (today.Date <= paydayThisMonth.Date)
        {
            var prevMonth = paydayThisMonth.AddMonths(-1);
            var start = new DateTime(prevMonth.Year, prevMonth.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            return (start, paydayThisMonth);
        }
        else
        {
            var nextMonth = paydayThisMonth.AddMonths(1);
            var start = paydayThisMonth.AddDays(1);
            var end = new DateTime(nextMonth.Year, nextMonth.Month, paydayDay, 0, 0, 0, DateTimeKind.Utc);
            return (start, end);
        }
    }

    private static List<string> CalculateMissedInstances(
        ChoreCreated chore,
        DateTime? lastCompleted,
        DateTime periodStart,
        DateTime today,
        List<ChoreCompleted>? completionsInPeriod,
        List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var frequency = chore.Frequency;
        if (frequency == null) return missed;

        var choreStart = chore.StartDate?.Date ?? periodStart;
        var effectiveStart = choreStart > periodStart ? choreStart : periodStart;

        // Only count as missed if grace period has passed
        var cutoffDate = today.AddDays(-GracePeriodDays);

        switch (frequency.Type.ToLower())
        {
            case "daily":
                missed = CalculateDailyMissed(effectiveStart, cutoffDate, completionsInPeriod, pauses);
                break;
            case "weekly":
                missed = CalculateWeeklyMissed(frequency.Days, effectiveStart, cutoffDate, completionsInPeriod, pauses);
                break;
            case "interval":
                missed = CalculateIntervalMissed(frequency.IntervalDays ?? 1, effectiveStart, cutoffDate, lastCompleted, completionsInPeriod, pauses);
                break;
        }

        return missed;
    }

    private static List<string> CalculateDailyMissed(DateTime start, DateTime cutoff, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        for (var date = start; date <= cutoff; date = date.AddDays(1))
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

    private static List<string> CalculateWeeklyMissed(string[]? days, DateTime start, DateTime cutoff, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).ToHashSet() ?? new HashSet<DateTime>();

        if (days == null || days.Length == 0)
        {
            // Weekly anytime - one completion per week required
            var currentWeek = GetMondayOfWeek(start);
            var cutoffWeek = GetMondayOfWeek(cutoff);

            while (currentWeek <= cutoffWeek)
            {
                var weekEnd = currentWeek.AddDays(6);
                var completedThisWeek = completedDates.Any(d => d >= currentWeek && d <= weekEnd);
                // Exempt the week if a pause window overlaps any day of it.
                var pausedThisWeek = pauses.CoversAnyInRange(currentWeek, weekEnd);

                if (!completedThisWeek && !pausedThisWeek && weekEnd <= cutoff)
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
            // Specific days required
            var requiredDays = days.Select(d => Enum.Parse<DayOfWeek>(d, ignoreCase: true)).ToHashSet();

            for (var date = start; date <= cutoff; date = date.AddDays(1))
            {
                if (requiredDays.Contains(date.DayOfWeek) && !completedDates.Contains(date) && !pauses.CoversDate(date))
                {
                    missed.Add(date.ToString("yyyy-MM-dd"));
                }
            }
        }

        return missed;
    }

    private static List<string> CalculateIntervalMissed(int intervalDays, DateTime start, DateTime cutoff, DateTime? lastCompleted, List<ChoreCompleted>? completions, List<PauseWindow>? pauses)
    {
        var missed = new List<string>();
        var completedDates = completions?.Select(c => c.CompletedAt.Date).OrderBy(d => d).ToList() ?? new List<DateTime>();

        var lastDue = lastCompleted?.Date ?? start.AddDays(-1);
        var idx = 0;

        while (true)
        {
            var nextDue = lastDue.AddDays(intervalDays);
            if (nextDue > cutoff) break;

            // Check if completed on or before next due date
            while (idx < completedDates.Count && completedDates[idx] <= nextDue)
            {
                lastDue = completedDates[idx];
                idx++;
                nextDue = lastDue.AddDays(intervalDays);
                if (nextDue > cutoff) break;
            }

            if (nextDue > cutoff) break;

            // Advance the schedule regardless, but only record a miss when the
            // due date isn't within a pause window.
            if (!pauses.CoversDate(nextDue))
            {
                missed.Add(nextDue.ToString("yyyy-MM-dd"));
            }
            lastDue = nextDue;
        }

        return missed;
    }
}
