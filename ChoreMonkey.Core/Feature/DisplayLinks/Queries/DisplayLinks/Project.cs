using ChoreMonkey.Events;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLinks;

public record DisplayLinkEntry(Guid LinkId, string Label, string CreatedAt);
public record DisplayLinksResponse(List<DisplayLinkEntry> Links);

/// <summary>The Display Links view: a household's live (not revoked) display links, oldest first. The secret is not in the events; the adapter adds each link's address from the vault.</summary>
public static class DisplayLinksView
{
    public static DisplayLinksResponse Project(IReadOnlyList<object> history)
    {
        var revoked = history.OfType<DisplayLinkRevoked>().Select(e => e.LinkId).ToHashSet();
        var links = history.OfType<DisplayLinkCreated>()
            .Where(e => !revoked.Contains(e.LinkId))
            .Select(e => new DisplayLinkEntry(e.LinkId, e.Label, e.TimestampUtc))
            .ToList();
        return new DisplayLinksResponse(links);
    }
}
