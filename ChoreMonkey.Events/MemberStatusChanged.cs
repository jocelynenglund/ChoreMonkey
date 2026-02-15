namespace ChoreMonkey.Events;

public record MemberStatusChanged(
    Guid MemberId,
    Guid HouseholdId,
    string Status
) : EventBase;
