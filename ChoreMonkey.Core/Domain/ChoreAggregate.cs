using System;

namespace ChoreMonkey.Core.Domain;

internal class ChoreAggregate
{
    public static string StreamId(Guid householdId) => $"chores-{householdId}";
}
