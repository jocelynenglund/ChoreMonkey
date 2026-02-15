namespace ChoreMonkey.Events;

public record ActivityRecorded(
    Guid ActivityId,
    Guid HouseholdId,
    string Type,              // "completion" | "member_joined" | "chore_assigned" | "nickname_changed" | "status_changed"
    string Text,              // Pre-rendered display text, e.g. "Luna completed Take out trash"
    Guid? ActorId,            // Who performed the action
    string? ActorNickname,    // Nickname at time of action
    Guid? ChoreId,            // Related chore if any
    string? ChoreName,        // Chore name at time of action
    string? ExtraJson         // Optional JSON for additional context (assignees, old/new values, etc.)
) : EventBase;
