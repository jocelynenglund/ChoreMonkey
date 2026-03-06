namespace ChoreMonkey.Core.Domain;

internal class SalaryReportsAggregate
{
    public static string StreamId(Guid householdId) => $"salary-{householdId}";
}
