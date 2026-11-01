using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Salary.Commands.SetMemberSalary;

/// <summary>Set Member Salary: records a MemberSalarySet. Nothing is checked (no member, no limits); the latest one wins in the views.</summary>
public static class SetMemberSalarySlice
{
    public static Decision Decide(IReadOnlyList<object> history, SetMemberSalaryCommand cmd, DateTime now) =>
        Decision.Accept(new MemberSalarySet(
            cmd.HouseholdId,
            cmd.MemberId,
            cmd.BaseSalary,
            cmd.DeductionMultiplier,
            cmd.BonusMultiplier,
            now));
}
