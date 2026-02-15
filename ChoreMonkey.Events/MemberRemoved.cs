namespace ChoreMonkey.Events;

public record MemberRemoved(
    Guid HouseholdId,
    Guid MemberId,
    Guid RemovedByMemberId,
    string Nickname) : EventBase;
