namespace ChoreMonkey.Events;

/// <summary>
/// Cancels a previously created pause window (by PauseId). After removal the
/// window no longer exempts any missed instances.
/// </summary>
public record ChorePauseRemoved(
    Guid PauseId,
    Guid ChoreId,
    Guid HouseholdId) : EventBase;
