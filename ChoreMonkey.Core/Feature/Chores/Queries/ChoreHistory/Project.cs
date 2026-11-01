using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Chores.Queries.ChoreHistory;

/// <summary>The ChoreHistory view, folded from the events by the slice itself. The handler is only the store adapter.</summary>
public static class ChoreHistoryView
{
    public static ChoreHistoryResponse Project(IReadOnlyList<object> history, Guid choreId)
    {
        var events = history;

        var completions = events.OfType<ChoreCompleted>()
            .Where(e => e.ChoreId == choreId)
            .OrderByDescending(e => e.CompletedAt)
            .Select(e => new CompletionDto(e.CompletedByMemberId, e.CompletedAt))
            .ToList();

        return new ChoreHistoryResponse(completions);
    }
}

