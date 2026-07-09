namespace ChoreMonkey.Events;

/// <summary>
/// A chore is paused for a date range. Missed instances that fall within
/// [PauseStart, PauseEnd] (inclusive) don't count as overdue and incur no
/// salary deduction. A chore can have multiple (even overlapping) pause
/// windows; each is identified by PauseId so it can be removed independently.
/// </summary>
public record ChorePaused(
    Guid PauseId,
    Guid ChoreId,
    Guid HouseholdId,
    DateTime PauseStart,
    DateTime PauseEnd,
    string? Reason = null) : EventBase;
