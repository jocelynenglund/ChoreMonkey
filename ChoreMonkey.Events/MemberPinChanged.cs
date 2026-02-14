namespace ChoreMonkey.Events;

public record MemberPinChanged(Guid HouseholdId, string NewMemberPinHash) : EventBase;
