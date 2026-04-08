namespace ChoreMonkey.Core.Domain;

internal class SalaryAggregate
{
    public static string StreamId(Guid householdId) => $"salary-{householdId}";
}
