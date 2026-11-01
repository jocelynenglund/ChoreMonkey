using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Invites.Commands.GenerateInvite;

/// <summary>
/// Generate Invite: an invite link for the household. Nothing is checked (not even that the household exists).
/// The adapter supplies the new invite's id.
/// </summary>
public static class GenerateInviteSlice
{
    public static Decision Decide(IReadOnlyList<object> history, GenerateInviteCommand cmd, Guid inviteId)
    {
        // Link format: /join/{householdId}/{inviteId} - frontend will prepend base URL
        var link = $"/join/{cmd.HouseholdId}/{inviteId}";
        return Decision.Accept(new InviteGenerated(cmd.HouseholdId, inviteId, link));
    }
}
