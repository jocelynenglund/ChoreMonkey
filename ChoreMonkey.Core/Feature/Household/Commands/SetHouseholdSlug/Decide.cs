using System.Text.RegularExpressions;
using ChoreMonkey.Events;
using ItsyBIT.Decisions;

namespace ChoreMonkey.Core.Feature.Household.Commands.SetHouseholdSlug;

/// <summary>
/// Set Household Slug: the slug (trimmed, lower-cased) must be well formed and not reserved (Invalid), the
/// household must exist (NotFound), the admin PIN must be right (Forbidden), and the slug must not belong to
/// another household (Conflict); the same household may claim it again. History is the household's events and
/// the slug's own stream (household-slug-{slug}) together. A claim writes SlugClaimed (slug stream) and
/// HouseholdSlugSet (household stream).
/// </summary>
public static class SetHouseholdSlugSlice
{
    public const string BadFormat = "Slug must be 3-30 characters, lowercase letters/numbers/hyphens, starting and ending with a letter or number.";
    public const string Reserved = "This slug is reserved and cannot be used.";
    public const string HouseholdNotFound = "Household not found.";
    public const string InvalidPin = "Invalid admin PIN.";
    public const string Taken = "This slug is already taken.";

    static readonly Regex SlugPattern = new(@"^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$", RegexOptions.Compiled);

    static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "h", "health", "household", "households",
        "invite", "join", "login", "logout", "register", "settings",
        "help", "about", "terms", "privacy"
    };

    public static string Normalize(string slug) => slug.Trim().ToLowerInvariant();

    /// <summary>Whether a normalized slug passes the format and reserved-word checks. The adapter uses it to
    /// decide whether it may touch the slug's stream at all.</summary>
    public static bool IsAcceptable(string normalizedSlug) =>
        SlugPattern.IsMatch(normalizedSlug) && !Blocklist.Contains(normalizedSlug);

    public static Decision Decide(IReadOnlyList<object> history, SetHouseholdSlugCommand cmd, bool adminPinVerified, DateTime now)
    {
        var slug = Normalize(cmd.Slug);

        if (!SlugPattern.IsMatch(slug)) return Decision.Invalid(BadFormat);
        if (Blocklist.Contains(slug)) return Decision.Invalid(Reserved);

        if (!history.OfType<HouseholdCreated>().Any()) return Decision.NotFound(HouseholdNotFound);
        if (!adminPinVerified) return Decision.Forbidden(InvalidPin);

        // Re-claiming is fine if it's the same household.
        var existingClaim = history.OfType<SlugClaimed>().FirstOrDefault();
        if (existingClaim != null && existingClaim.HouseholdId != cmd.HouseholdId) return Decision.Conflict(Taken);

        return Decision.Accept(
            new SlugClaimed(cmd.HouseholdId, slug, now),
            new HouseholdSlugSet(cmd.HouseholdId, slug, now));
    }
}
