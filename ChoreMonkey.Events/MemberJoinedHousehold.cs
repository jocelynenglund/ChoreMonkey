namespace ChoreMonkey.Events;

public record MemberJoinedHousehold(
    Guid MemberId,
    Guid HouseholdId,
    Guid InviteId,
    string Nickname
) : EventBase;
