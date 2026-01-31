using System;
using System.Collections.Generic;
using System.Text;

namespace ChoreMonkey.Core.Domain;

internal class HouseholdAggregate
{
    public static string StreamId(Guid householdId) => $"household-{householdId}";
}
