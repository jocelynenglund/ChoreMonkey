using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetChoreRates;

/// <summary>Set Chore Rates: records a ChoreRatesSet. Nothing is checked; a null rate clears that rate back to the chore's own default.</summary>
public static class SetChoreRatesSlice
{
    public static Decision Decide(IReadOnlyList<object> history, SetChoreRatesCommand cmd, DateTime now) =>
        Decision.Accept(new ChoreRatesSet(
            cmd.HouseholdId,
            cmd.ChoreId,
            cmd.DeductionRate,
            cmd.BonusRate,
            now));
}
