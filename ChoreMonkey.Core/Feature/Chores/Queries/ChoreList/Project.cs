using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Chores.Queries.ChoreList;

/// <summary>The ChoreList view, folded from the events by the slice itself. The handler is only the store adapter.</summary>
public static class ChoreListView
{
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    public static GetChoresResponse Project(IReadOnlyList<object> history, DateTime now)
    {
        // History is the chores', the household's and the salary events together; each type lives in one stream.
        var events = history;
        var householdEvents = history;
        var salaryEvents = history;
        
        // Get latest rates for each chore from salary stream
        var choreRates = salaryEvents.OfType<ChoreRatesSet>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        
        var today = now.Date;
        var weekStart = GetMondayOfWeek(today);
        var weekEnd = weekStart.AddDays(7);
        
        // Get all household members
        var allMemberIds = householdEvents.OfType<MemberJoinedHousehold>()
            .Select(e => e.MemberId)
            .ToArray();

        // Get all created chores, excluding deleted ones
        var deletedChoreIds = events.OfType<ChoreDeleted>()
            .Select(d => d.ChoreId)
            .ToHashSet();
        var createdChores = events.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        foreach (var update in events.OfType<ChoreUpdated>())
            if (createdChores.ContainsKey(update.ChoreId))
                createdChores[update.ChoreId] = createdChores[update.ChoreId] with
                {
                    DisplayName = update.DisplayName, Description = update.Description,
                    Frequency = update.Frequency, IsOptional = update.IsOptional,
                    StartDate = update.StartDate, IsRequired = update.IsRequired,
                    MissedDeduction = update.MissedDeduction,
                };
        var createdChoresList = createdChores.Values.ToList();
        
        // Get latest assignment for each chore
        var assignments = events.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Active pause windows per chore
        var chorePauses = ChorePauses.Build(events);

        // Get all completions grouped by chore
        var completionsByChore = events.OfType<ChoreCompleted>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var chores = createdChoresList
            .Select(e => {
                var assignment = assignments.GetValueOrDefault(e.ChoreId);
                var choreCompletions = completionsByChore.GetValueOrDefault(e.ChoreId) ?? new List<ChoreCompleted>();
                var lastCompletion = choreCompletions.OrderByDescending(c => c.CompletedAt).FirstOrDefault();
                var rates = choreRates.GetValueOrDefault(e.ChoreId);

                var pauses = chorePauses.GetValueOrDefault(e.ChoreId)
                    ?.OrderBy(p => p.Start)
                    .Select(p => new PauseWindowDto(p.PauseId, p.Start, p.End, p.Reason))
                    .ToList();

                var frequency = e.Frequency != null
                    ? new FrequencyDto(e.Frequency.Type, e.Frequency.Days, e.Frequency.IntervalDays)
                    : new FrequencyDto("once");
                
                // Build per-member completion status
                List<MemberCompletionDto>? memberCompletions = null;
                if (assignment?.AssignedToMemberIds != null || assignment?.AssignToAll == true)
                {
                    // Use all members if AssignToAll, otherwise use assigned members
                    var assignedMembers = assignment.AssignToAll == true 
                        ? allMemberIds 
                        : (assignment.AssignedToMemberIds ?? Array.Empty<Guid>());
                    memberCompletions = assignedMembers.Select(memberId => {
                        var memberCompletionsList = choreCompletions
                            .Where(c => c.CompletedByMemberId == memberId)
                            .ToList();
                        var memberLastCompletion = memberCompletionsList
                            .OrderByDescending(c => c.CompletedAt)
                            .FirstOrDefault();
                        var completedToday = memberLastCompletion?.CompletedAt.Date == today;
                        var completedThisWeek = memberCompletionsList
                            .Any(c => c.CompletedAt.Date >= weekStart && c.CompletedAt.Date < weekEnd);
                        return new MemberCompletionDto(
                            memberId,
                            completedToday,
                            completedThisWeek,
                            memberLastCompletion?.CompletedAt);
                    }).ToList();
                }
                    
                return new ChoreDto(
                    e.ChoreId, 
                    e.DisplayName, 
                    e.Description,
                    assignment?.AssignedToMemberIds,
                    assignment?.AssignToAll ?? false,
                    frequency,
                    lastCompletion?.CompletedAt,
                    lastCompletion?.CompletedByMemberId,
                    memberCompletions,
                    e.IsOptional,
                    e.IsRequired,
                    e.MissedDeduction,
                    rates?.DeductionRate,
                    rates?.BonusRate,
                    pauses);
            })
            .ToList();

        return new GetChoresResponse(chores);
    }
}

