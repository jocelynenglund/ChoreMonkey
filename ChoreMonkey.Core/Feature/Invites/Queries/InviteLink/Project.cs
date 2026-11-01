using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.Invites.Queries.InviteLink;

/// <summary>The InviteLink view: the household's latest invite, or null when none has been generated.</summary>
public static class InviteLinkView
{
    public static InviteLinkResponse? Project(IReadOnlyList<object> history)
    {
        var invite = history.OfType<InviteGenerated>().LastOrDefault();
        if (invite == null) return null;

        return new InviteLinkResponse(invite.HouseholdId, invite.InviteId, invite.Link);
    }
}
