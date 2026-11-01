using ChoreMonkey.Core.Feature.Household.Commands.CreateHousehold;
using ChoreMonkey.Core.Feature.Household.Commands.SetAdminPin;
using ChoreMonkey.Core.Feature.Household.Commands.SetHouseholdSlug;
using ChoreMonkey.Core.Feature.Household.Commands.SetMemberPin;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>
/// The Household chapter, commands: one test per scenario, the happy case first, then each refusal or branch.
/// PIN hashing and checking belong to the store adapter, so these scenarios hand Decide stand-in hashes and a
/// verdict ("the PIN was verified") rather than real PINs.
/// </summary>
public class HouseholdSpecs
{
    static readonly HistoryChapter Household = new("Household");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

    static readonly HouseholdCreated Created = new(Home, "Home", "admin-hash");

    // ── Create Household ─────────────────────────────────────────────────────

    static Decision Create(IReadOnlyList<object> history, CreateHouseholdCommand cmd) =>
        CreateHouseholdSlice.Decide(history, cmd, "admin-hash", null, Ann);

    [Fact]
    public void A_household_is_created_and_its_owner_joins() =>
        Household.Scenario("A household is created and its owner joins")
            .Given()
            .When("Create Household", new CreateHouseholdCommand(Home, "Home", 1234, "Mum"), Create)
            .Then(
                Event<HouseholdCreated>(new { householdId = Home, name = "Home", pinHash = "admin-hash" }),
                Event<MemberJoinedHousehold>(new { memberId = Ann, householdId = Home, inviteId = Guid.Empty, nickname = "Mum" }));

    [Fact]
    public void The_owner_is_called_Admin_unless_named() =>
        Household.Scenario("The owner is called Admin unless named")
            .Given()
            .When("Create Household", new CreateHouseholdCommand(Home, "Home", 1234), Create)
            .Then(
                Event<HouseholdCreated>(new { householdId = Home }),
                Event<MemberJoinedHousehold>(new { memberId = Ann, nickname = "Admin" }));

    [Fact]
    public void A_household_can_start_with_a_separate_member_pin() =>
        Household.Scenario("A household can start with a separate member PIN")
            .Given()
            .When("Create Household", new CreateHouseholdCommand(Home, "Home", 1234, "Mum", 5678),
                (h, c) => CreateHouseholdSlice.Decide(h, c, "admin-hash", "member-hash", Ann))
            .Then(
                Event<HouseholdCreated>(new { householdId = Home, pinHash = "admin-hash", memberPinHash = "member-hash" }),
                Event<MemberJoinedHousehold>(new { memberId = Ann }));

    // ── Set Admin Pin ────────────────────────────────────────────────────────

    static Decision SetAdmin(IReadOnlyList<object> history, SetAdminPinCommand cmd) =>
        SetAdminPinSlice.Decide(history, cmd, true, "new-admin-hash");

    [Fact]
    public void The_admin_pin_is_changed() =>
        Household.Scenario("The admin PIN is changed")
            .Given(Created)
            .When("Set Admin Pin", new SetAdminPinCommand(Home, 1234, 4321), SetAdmin)
            .Then(Event<AdminPinChanged>(new { householdId = Home, newPinHash = "new-admin-hash" }));

    [Fact]
    public void The_admin_pin_cant_be_changed_with_the_wrong_current_pin() =>
        Household.Scenario("The admin PIN can't be changed with the wrong current PIN")
            .Given(Created)
            .When("Set Admin Pin", new SetAdminPinCommand(Home, 9999, 4321), (h, c) => SetAdminPinSlice.Decide(h, c, false, "new-admin-hash"))
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void The_admin_pin_of_an_unknown_household_cant_be_changed() =>
        Household.Scenario("The admin PIN of an unknown household can't be changed")
            .Given()
            .When("Set Admin Pin", new SetAdminPinCommand(Home, 1234, 4321), SetAdmin)
            .ThenNothing("Household not found", "Forbidden");

    // ── Set Member Pin ───────────────────────────────────────────────────────

    static Decision SetMember(IReadOnlyList<object> history, SetMemberPinCommand cmd) =>
        SetMemberPinSlice.Decide(history, cmd, true, "new-member-hash");

    [Fact]
    public void The_member_pin_is_set() =>
        Household.Scenario("The member PIN is set")
            .Given(Created)
            .When("Set Member Pin", new SetMemberPinCommand(Home, 1234, 5678), SetMember)
            .Then(Event<MemberPinChanged>(new { householdId = Home, newMemberPinHash = "new-member-hash" }));

    [Fact]
    public void The_member_pin_cant_be_set_with_the_wrong_admin_pin() =>
        Household.Scenario("The member PIN can't be set with the wrong admin PIN")
            .Given(Created)
            .When("Set Member Pin", new SetMemberPinCommand(Home, 9999, 5678), (h, c) => SetMemberPinSlice.Decide(h, c, false, "new-member-hash"))
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void The_member_pin_of_an_unknown_household_cant_be_set() =>
        Household.Scenario("The member PIN of an unknown household can't be set")
            .Given()
            .When("Set Member Pin", new SetMemberPinCommand(Home, 1234, 5678), SetMember)
            .ThenNothing("Household not found", "Forbidden");

    // ── Set Household Slug ───────────────────────────────────────────────────

    static Decision Slug(IReadOnlyList<object> history, SetHouseholdSlugCommand cmd) =>
        SetHouseholdSlugSlice.Decide(history, cmd, true, Now);

    static SetHouseholdSlugCommand ClaimSlug(string slug) => new(Home, slug, "1234");

    [Fact]
    public void A_slug_is_claimed() =>
        Household.Scenario("A slug is claimed")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("the-smiths"), Slug)
            .Then(
                Event<SlugClaimed>(new { householdId = Home, slug = "the-smiths", claimedAt = Now }),
                Event<HouseholdSlugSet>(new { householdId = Home, slug = "the-smiths", setAt = Now }));

    [Fact]
    public void A_slug_is_trimmed_and_lower_cased() =>
        Household.Scenario("A slug is trimmed and lower-cased")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("  The-Smiths "), Slug)
            .Then(Event<SlugClaimed>(new { slug = "the-smiths" }), Event<HouseholdSlugSet>(new { slug = "the-smiths" }));

    [Fact]
    public void The_same_household_can_claim_its_slug_again() =>
        Household.Scenario("The same household can claim its slug again")
            .Given(Created, new SlugClaimed(Home, "the-smiths", Now.AddDays(-1)), new HouseholdSlugSet(Home, "the-smiths", Now.AddDays(-1)))
            .When("Set Household Slug", ClaimSlug("the-smiths"), Slug)
            .Then(Event<SlugClaimed>(new { householdId = Home, slug = "the-smiths", claimedAt = Now }), Event<HouseholdSlugSet>(new { slug = "the-smiths" }));

    [Fact]
    public void A_slug_another_household_has_is_taken() =>
        Household.Scenario("A slug another household has is taken")
            .Given(Created, new SlugClaimed(Other, "the-smiths", Now.AddDays(-1)))
            .When("Set Household Slug", ClaimSlug("the-smiths"), Slug)
            .ThenNothing("This slug is already taken.", "Conflict");

    [Fact]
    public void A_slug_must_be_well_formed() =>
        Household.Scenario("A slug must be 3-30 lowercase letters, numbers or hyphens")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("-ab-"), Slug)
            .ThenNothing("Slug must be 3-30 characters, lowercase letters/numbers/hyphens, starting and ending with a letter or number.", "Invalid");

    [Fact]
    public void A_slug_cant_be_too_short() =>
        Household.Scenario("A slug can't be too short")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("ab"), Slug)
            .ThenNothing("Slug must be 3-30 characters, lowercase letters/numbers/hyphens, starting and ending with a letter or number.", "Invalid");

    [Fact]
    public void A_slug_cant_contain_a_path() =>
        Household.Scenario("A slug can't contain a path")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("../other"), Slug)
            .ThenNothing("Slug must be 3-30 characters, lowercase letters/numbers/hyphens, starting and ending with a letter or number.", "Invalid");

    [Fact]
    public void A_reserved_slug_cant_be_used() =>
        Household.Scenario("A reserved slug can't be used")
            .Given(Created)
            .When("Set Household Slug", ClaimSlug("Admin"), Slug)
            .ThenNothing("This slug is reserved and cannot be used.", "Invalid");

    [Fact]
    public void A_slug_cant_be_claimed_for_an_unknown_household() =>
        Household.Scenario("A slug can't be claimed for an unknown household")
            .Given()
            .When("Set Household Slug", ClaimSlug("the-smiths"), Slug)
            .ThenNothing("Household not found.", "NotFound");

    [Fact]
    public void A_slug_cant_be_claimed_with_the_wrong_admin_pin() =>
        Household.Scenario("A slug can't be claimed with the wrong admin PIN")
            .Given(Created)
            .When("Set Household Slug", new SetHouseholdSlugCommand(Home, "the-smiths", "9999"),
                (h, c) => SetHouseholdSlugSlice.Decide(h, c, false, Now))
            .ThenNothing("Invalid admin PIN.", "Forbidden");
}
