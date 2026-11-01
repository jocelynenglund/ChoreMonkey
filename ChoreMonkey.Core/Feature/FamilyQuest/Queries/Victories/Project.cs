using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Victories;

/// <summary>The victories (the newest completions, with who did them), folded from the events by the slice itself.</summary>
public static class VictoriesView
{
    public static GetVictoriesResponse Project(IReadOnlyList<object> history, int limit)
    {
        var nicknames = new Dictionary<Guid, string>();
        foreach (var e in history)
        {
            switch (e)
            {
                case MemberJoinedHousehold j: nicknames[j.MemberId] = j.Nickname; break;
                case MemberNicknameChanged n: nicknames[n.MemberId] = n.NewNickname; break;
            }
        }

        var choreNames = history.OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        var victories = history.OfType<ChoreCompleted>()
            .OrderByDescending(c => c.CompletedAt)
            .Take(limit)
            .Select(c => new VictoryDto(
                c.ChoreId,
                c.CompletedByMemberId,
                nicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown"),
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown chore"),
                c.CompletedAt))
            .ToList();

        return new GetVictoriesResponse(victories);
    }
}
