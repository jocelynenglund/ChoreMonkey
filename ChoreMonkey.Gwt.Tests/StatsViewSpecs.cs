using ChoreMonkey.Core.Feature.Stats.Queries.PlatformStats;
using ChoreMonkey.Core.Security;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Stats chapter, views: the platform stats across every household. "Now" is Wednesday 2026-03-18 10:00 UTC.</summary>
public class StatsViewSpecs
{
    static readonly HistoryChapter Stats = new("Stats");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

    static readonly string AdminPinHash = PinHasher.HashPin(1234);

    // A household's creation time is read from its event. Unspecified kind keeps the expected value zone-free.
    static readonly HouseholdCreated Home2026 = new(Home, "Home", AdminPinHash) { TimestampUtc = "2026-01-05T10:00:00" };
    static readonly HouseholdCreated OtherHousehold = new(Other, "Other", AdminPinHash) { TimestampUtc = "2026-02-20T10:00:00" };

    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Laundry = new(LaundryId, Home, "Laundry", "Wash clothes", new ChoreFrequency("weekly"), StartDate: new DateTime(2026, 3, 1));
    static MemberJoinedHousehold Joined(Guid member, string nickname) => new(member, Home, Guid.NewGuid(), nickname);

    // The stats read every household's stream, so the view takes one history per household. The scenario's Given
    // lists each household from its HouseholdCreated on, with its chores after it, and this splits it the same way.
    static IReadOnlyList<IReadOnlyList<object>> ByHousehold(IReadOnlyList<object> h)
    {
        var streams = new List<List<object>>();
        foreach (var e in h)
        {
            if (e is HouseholdCreated || streams.Count == 0) streams.Add([]);
            streams[^1].Add(e);
        }
        return streams;
    }

    static PlatformStatsResponse Platform(IReadOnlyList<object> h) => PlatformStatsView.Project(ByHousehold(h), null, null, Now);

    [Fact]
    public void With_no_households_the_stats_are_all_zero() =>
        Stats.Scenario("With no households the stats are all zero")
            .Given()
            .ThenView("Platform Stats", Platform, new
            {
                totalHouseholds = 0,
                totalMembers = 0,
                totalChores = 0,
                households = Array.Empty<object>(),
            });

    [Fact]
    public void A_household_counts_its_members_and_chores() =>
        Stats.Scenario("A household counts its members and chores")
            .Given(Home2026, Joined(Ann, "Ann"), Joined(Bob, "Bob"), Dishes, Laundry)
            .ThenView("Platform Stats", Platform, new
            {
                totalHouseholds = 1,
                totalMembers = 2,
                totalChores = 2,
                households = new[] { new { householdId = Home, name = "Home", memberCount = 2, choreCount = 2 } },
            });

    [Fact]
    public void A_removed_member_and_a_deleted_chore_are_not_counted() =>
        Stats.Scenario("A removed member and a deleted chore are not counted")
            .Given(Home2026, Joined(Ann, "Ann"), Joined(Bob, "Bob"), new MemberRemoved(Home, Bob, Ann, "Bob"), Dishes, Laundry, new ChoreDeleted(LaundryId, Home, Ann))
            .ThenView("Platform Stats", Platform, new
            {
                totalMembers = 1,
                totalChores = 1,
                households = new[] { new { householdId = Home, memberCount = 1, choreCount = 1 } },
            });

    [Fact]
    public void Households_are_listed_newest_first() =>
        Stats.Scenario("Households are listed newest first")
            .Given(Home2026, Joined(Ann, "Ann"), Dishes, OtherHousehold, new MemberJoinedHousehold(Bob, Other, Guid.NewGuid(), "Bob"), new ChoreCreated(LaundryId, Other, "Laundry", "Wash clothes"))
            .ThenView("Platform Stats", Platform, new
            {
                totalHouseholds = 2,
                totalMembers = 2,
                totalChores = 2,
                households = new[]
                {
                    new { householdId = Other, name = "Other", memberCount = 1, choreCount = 1, createdAt = new DateTime(2026, 2, 20, 10, 0, 0) },
                    new { householdId = Home, name = "Home", memberCount = 1, choreCount = 1, createdAt = new DateTime(2026, 1, 5, 10, 0, 0) },
                },
            });

    [Fact]
    public void A_household_with_an_unreadable_creation_time_is_dated_now() =>
        Stats.Scenario("A household with an unreadable creation time is dated now")
            .Given(new HouseholdCreated(Home, "Home", AdminPinHash) { TimestampUtc = "not a date" }, Joined(Ann, "Ann"))
            .ThenView("Platform Stats", Platform, new
            {
                households = new[] { new { householdId = Home, createdAt = Now } },
            });

    [Fact]
    public void A_stream_without_a_household_created_is_not_a_household() =>
        Stats.Scenario("A stream without a household created is not a household")
            .Given(Joined(Ann, "Ann"), Dishes)
            .ThenView("Platform Stats", Platform, new
            {
                totalHouseholds = 0,
                totalMembers = 0,
                totalChores = 0,
                households = Array.Empty<object>(),
            });
}
