using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Chores.Commands.AcknowledgeMissed;

/// <summary>Acknowledge Missed: records that a member didn't do a chore for a period. Nothing is checked.</summary>
public static class AcknowledgeMissedSlice
{
    public static Decision Decide(IReadOnlyList<object> history, AcknowledgeMissedCommand cmd) =>
        Decision.Accept(new ChoreMissedAcknowledged(cmd.ChoreId, cmd.MemberId, cmd.Period));
}
