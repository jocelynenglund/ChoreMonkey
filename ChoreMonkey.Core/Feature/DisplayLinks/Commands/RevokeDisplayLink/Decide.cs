using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Commands.RevokeDisplayLink;

public record RevokeDisplayLinkCommand(Guid HouseholdId, Guid LinkId);

/// <summary>Revoke Display Link: the caller must be the household's admin (Forbidden) and the link must be live (NotFound).</summary>
public static class RevokeDisplayLinkSlice
{
    public const string NotFound = "Display link not found";

    public static Decision Decide(IReadOnlyList<object> history, RevokeDisplayLinkCommand cmd, bool adminVerified)
    {
        if (!history.OfType<HouseholdCreated>().Any()) return Decision.NotFound("Household not found");
        if (!adminVerified) return Decision.Forbidden("Admin access required");

        var made = history.OfType<DisplayLinkCreated>().Any(e => e.LinkId == cmd.LinkId);
        var revoked = history.OfType<DisplayLinkRevoked>().Any(e => e.LinkId == cmd.LinkId);
        if (!made || revoked) return Decision.NotFound(NotFound);

        return Decision.Accept(new DisplayLinkRevoked(cmd.HouseholdId, cmd.LinkId));
    }
}
