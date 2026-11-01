using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetPayday;

/// <summary>Set Payday: the day of the month must be 1 to 28 (Invalid), otherwise PaydayConfigured is recorded.</summary>
public static class SetPaydaySlice
{
    public const string OutOfRange = "Payday must be between 1 and 28.";

    public static Decision Decide(IReadOnlyList<object> history, SetPaydayCommand cmd)
    {
        if (cmd.PaydayDayOfMonth < 1 || cmd.PaydayDayOfMonth > 28)
            return Decision.Invalid(OutOfRange);

        return Decision.Accept(new PaydayConfigured(cmd.HouseholdId, cmd.PaydayDayOfMonth));
    }
}
