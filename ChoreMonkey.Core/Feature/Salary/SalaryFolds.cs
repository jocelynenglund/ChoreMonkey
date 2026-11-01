using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Salary;

/// <summary>
/// The small folds the salary slices share (who is in the household, which chores are live and who they are
/// assigned to). Pure functions over a history that holds the household, chores and salary events together.
/// </summary>
public static class SalaryFolds
{
    /// <summary>memberId → current nickname, for members who joined and were not removed (renames honoured).</summary>
    public static Dictionary<Guid, string> ActiveMembers(IReadOnlyList<object> history)
    {
        var members = new Dictionary<Guid, string>();
        foreach (var e in history)
        {
            switch (e)
            {
                case MemberJoinedHousehold joined:
                    members[joined.MemberId] = joined.Nickname;
                    break;
                case MemberNicknameChanged changed:
                    if (members.ContainsKey(changed.MemberId))
                        members[changed.MemberId] = changed.NewNickname;
                    break;
                case MemberRemoved removed:
                    members.Remove(removed.MemberId);
                    break;
            }
        }
        return members;
    }

    /// <summary>choreId → the chore as created and then updated; deleted chores are left out.</summary>
    public static Dictionary<Guid, ChoreCreated> LiveChores(IReadOnlyList<object> history)
    {
        var deletedChoreIds = history.OfType<ChoreDeleted>().Select(e => e.ChoreId).ToHashSet();
        var chores = history.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        foreach (var update in history.OfType<ChoreUpdated>())
            if (chores.ContainsKey(update.ChoreId))
                chores[update.ChoreId] = chores[update.ChoreId] with
                {
                    DisplayName = update.DisplayName, Description = update.Description,
                    Frequency = update.Frequency, IsOptional = update.IsOptional,
                    StartDate = update.StartDate, IsRequired = update.IsRequired,
                    MissedDeduction = update.MissedDeduction,
                };
        return chores;
    }

    /// <summary>choreId → its latest assignment.</summary>
    public static Dictionary<Guid, ChoreAssigned> Assignments(IReadOnlyList<object> history) =>
        history.OfType<ChoreAssigned>().GroupBy(e => e.ChoreId).ToDictionary(g => g.Key, g => g.Last());

    /// <summary>memberId → latest salary settings.</summary>
    public static Dictionary<Guid, MemberSalarySet> SalaryConfigs(IReadOnlyList<object> history) =>
        history.OfType<MemberSalarySet>().GroupBy(e => e.MemberId).ToDictionary(g => g.Key, g => g.Last());

    /// <summary>choreId → latest rates.</summary>
    public static Dictionary<Guid, ChoreRatesSet> ChoreRates(IReadOnlyList<object> history) =>
        history.OfType<ChoreRatesSet>().GroupBy(e => e.ChoreId).ToDictionary(g => g.Key, g => g.Last());

    /// <summary>The configured payday (last one wins), 25 when none was set.</summary>
    public static int Payday(IReadOnlyList<object> history) =>
        history.OfType<PaydayConfigured>().LastOrDefault()?.PaydayDayOfMonth ?? 25;

    /// <summary>choreId, memberId → the member's latest completion of the chore.</summary>
    public static Dictionary<(Guid ChoreId, Guid MemberId), ChoreCompleted> LastCompletions(IReadOnlyList<object> history) =>
        history.OfType<ChoreCompleted>()
            .GroupBy(e => (e.ChoreId, e.CompletedByMemberId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CompletedAt).First());
}
