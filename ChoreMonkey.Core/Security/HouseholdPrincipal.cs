namespace ChoreMonkey.Core.Security;

public sealed record HouseholdPrincipal(Guid HouseholdId, Guid? MemberId, bool IsAdmin);
