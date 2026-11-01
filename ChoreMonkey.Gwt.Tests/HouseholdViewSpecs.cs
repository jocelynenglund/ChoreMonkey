using ChoreMonkey.Core.Feature.Household.Queries.AccessHousehold;
using ChoreMonkey.Core.Feature.Household.Queries.GetHouseholdBySlug;
using ChoreMonkey.Core.Feature.Household.Queries.HouseholdName;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Household chapter, views. Access Household is the sign-in check: the adapter does the real PIN
/// hashing, so here a PIN "matches" a hash when the hash is the stand-in "hash-of-{pin}".
/// </summary>
public class HouseholdViewSpecs
{
    static readonly HistoryChapter Household = new("Household");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

    static readonly HouseholdCreated Created = new(Home, "Home", "hash-of-1234");
    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.Empty, "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");

    // ── Household Name ───────────────────────────────────────────────────────

    static HouseholdNameResponse Name(IReadOnlyList<object> h) => HouseholdNameView.Project(h, Home);

    [Fact]
    public void The_name_shows_the_household_name() =>
        Household.Scenario("The name shows the household name")
            .Given(Created)
            .ThenView("Household Name", Name, new { householdId = Home, householdName = "Home", slug = (string?)null });

    [Fact]
    public void The_name_shows_the_latest_slug() =>
        Household.Scenario("The name shows the latest slug")
            .Given(Created, new HouseholdSlugSet(Home, "the-smiths", Now), new HouseholdSlugSet(Home, "smiths-home", Now))
            .ThenView("Household Name", Name, new { householdName = "Home", slug = "smiths-home" });

    [Fact]
    public void An_unknown_household_is_called_Unknown() =>
        Household.Scenario("An unknown household is called Unknown")
            .Given()
            .ThenView("Household Name", Name, new { householdId = Home, householdName = "Unknown", slug = (string?)null });

    // ── Get Household By Slug ────────────────────────────────────────────────

    [Fact]
    public void A_slug_shows_its_household_and_active_members() =>
        Household.Scenario("A slug shows its household and how many members are active")
            .Given(new SlugClaimed(Home, "the-smiths", Now), Created, AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("Get Household By Slug", GetHouseholdBySlugView.Project, new { householdId = Home, name = "Home", memberCount = 1 });

    [Fact]
    public void An_unclaimed_slug_shows_nothing() =>
        Household.Scenario("An unclaimed slug shows nothing")
            .Given()
            .ThenView("Get Household By Slug", GetHouseholdBySlugView.Project, null);

    [Fact]
    public void A_slug_whose_household_has_no_events_shows_nothing() =>
        Household.Scenario("A slug whose household has no events shows nothing")
            .Given(new SlugClaimed(Home, "the-smiths", Now))
            .ThenView("Get Household By Slug", GetHouseholdBySlugView.Project, null);

    // ── Access Household ─────────────────────────────────────────────────────

    static Func<IReadOnlyList<object>, AccessHouseholdResponse> Access(int pin) =>
        h => AccessHouseholdView.Project(h, Home, hash => hash == $"hash-of-{pin}");

    [Fact]
    public void The_admin_pin_opens_the_household_as_admin() =>
        Household.Scenario("The admin PIN opens the household as admin")
            .Given(Created)
            .ThenView("Access Household", Access(1234), new { success = true, householdId = Home, householdName = "Home", isAdmin = true });

    [Fact]
    public void The_member_pin_opens_the_household_as_a_member() =>
        Household.Scenario("The member PIN opens the household as a member")
            .Given(new HouseholdCreated(Home, "Home", "hash-of-1234", "hash-of-5678"))
            .ThenView("Access Household", Access(5678), new { success = true, householdName = "Home", isAdmin = false });

    [Fact]
    public void A_changed_member_pin_replaces_the_one_from_creation() =>
        Household.Scenario("A changed member PIN replaces the one from creation")
            .Given(new HouseholdCreated(Home, "Home", "hash-of-1234", "hash-of-5678"), new MemberPinChanged(Home, "hash-of-8765"))
            .ThenView("Access Household", Access(5678), new { success = false, householdName = (string?)null, isAdmin = false });

    [Fact]
    public void The_new_member_pin_opens_the_household() =>
        Household.Scenario("The new member PIN opens the household")
            .Given(Created, new MemberPinChanged(Home, "hash-of-8765"))
            .ThenView("Access Household", Access(8765), new { success = true, householdName = "Home", isAdmin = false });

    [Fact]
    public void A_changed_admin_pin_replaces_the_old_one() =>
        Household.Scenario("A changed admin PIN replaces the old one")
            .Given(Created, new AdminPinChanged(Home, "hash-of-4321"))
            .ThenView("Access Household", Access(1234), new { success = false, isAdmin = false });

    [Fact]
    public void The_new_admin_pin_opens_the_household_as_admin() =>
        Household.Scenario("The new admin PIN opens the household as admin")
            .Given(Created, new AdminPinChanged(Home, "hash-of-4321"))
            .ThenView("Access Household", Access(4321), new { success = true, isAdmin = true });

    [Fact]
    public void A_wrong_pin_opens_nothing() =>
        Household.Scenario("A wrong PIN opens nothing")
            .Given(new HouseholdCreated(Home, "Home", "hash-of-1234", "hash-of-5678"))
            .ThenView("Access Household", Access(9999), new { success = false, householdId = Home, householdName = (string?)null, isAdmin = false });

    [Fact]
    public void Without_a_member_pin_only_the_admin_pin_works() =>
        Household.Scenario("Without a member PIN only the admin PIN works")
            .Given(Created)
            .ThenView("Access Household", Access(5678), new { success = false, isAdmin = false });

    [Fact]
    public void An_unknown_household_cant_be_opened() =>
        Household.Scenario("An unknown household can't be opened")
            .Given()
            .ThenView("Access Household", Access(1234), new { success = false, householdId = Home, householdName = (string?)null, isAdmin = false });
}
