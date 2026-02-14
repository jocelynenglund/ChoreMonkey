namespace ChoreMonkey.Events;

/// <summary>
/// Member explicitly acknowledges they didn't do a chore for a period.
/// Period format: "2024-02-13" for daily, "2024-W06" for weekly
/// </summary>
public record ChoreMissedAcknowledged(
    Guid ChoreId,
    Guid MemberId,
    string Period) : EventBase;
