using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Commands.MakeDisplayLink;

public record MakeDisplayLinkCommand(Guid HouseholdId, string? Label);

/// <summary>
/// Make Display Link: the household must exist (NotFound) and the caller must be its admin (Forbidden, the
/// adapter checks the PIN or session). The label is trimmed, defaults to "barrio" and may be 40 characters at most
/// (Invalid). A household may hold five live links (Conflict). The link's secret is made and kept by the adapter;
/// only the link's id and label become an event.
/// </summary>
public static class MakeDisplayLinkSlice
{
    public const string DefaultLabel = "barrio";
    public const int MaxLabel = 40;
    public const int MaxLinks = 5;
    public const string LabelTooLong = "A label can be 40 characters at most.";
    public const string TooMany = "A household can have five display links. Revoke one first.";

    public static Decision Decide(IReadOnlyList<object> history, MakeDisplayLinkCommand cmd, bool adminVerified, Guid linkId)
    {
        if (!history.OfType<HouseholdCreated>().Any()) return Decision.NotFound("Household not found");
        if (!adminVerified) return Decision.Forbidden("Admin access required");

        var label = (cmd.Label ?? "").Trim();
        if (label.Length == 0) label = DefaultLabel;
        if (label.Length > MaxLabel) return Decision.Invalid(LabelTooLong);

        var revoked = history.OfType<DisplayLinkRevoked>().Select(e => e.LinkId).ToHashSet();
        var live = history.OfType<DisplayLinkCreated>().Count(e => !revoked.Contains(e.LinkId));
        if (live >= MaxLinks) return Decision.Conflict(TooMany);

        return Decision.Accept(new DisplayLinkCreated(cmd.HouseholdId, linkId, label));
    }
}
