namespace ChoreMonkey.Events;

public record MemberNicknameChanged(
    Guid MemberId,
    Guid HouseholdId,
    string OldNickname,
    string NewNickname
) : EventBase;
