using ChoreMonkey.Core.Feature.FamilyQuest.Queries.Party;
using ChoreMonkey.Core.Feature.FamilyQuest.Queries.Quests;
using ChoreMonkey.Core.Feature.FamilyQuest.Queries.Victories;
using ChoreMonkey.Core.Feature.FamilyQuest.Queries.XP;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Family Quest chapter, views: quests, victories, the party and the XP tiers. "Now" is Wednesday 2026-03-18 10:00 UTC.</summary>
public class FamilyQuestViewSpecs
{
    static readonly HistoryChapter FamilyQuest = new("Family Quest");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid BinsId = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    static readonly Guid BossId = Guid.Parse("c0000000-0000-0000-0000-000000000004");
    static readonly Guid BonusId = Guid.Parse("c0000000-0000-0000-0000-000000000005");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Today = Now.Date;
    static readonly DateTime Yesterday = Today.AddDays(-1);

    // Chores: a daily one, a weekly one, an interval one, a one-off (boss) and an optional one (bonus).
    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Laundry = new(LaundryId, Home, "Laundry", "Wash clothes", new ChoreFrequency("weekly"), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Bins = new(BinsId, Home, "Bins", "Take out the bins", new ChoreFrequency("interval", null, 3), StartDate: new DateTime(2026, 3, 1));
    static readonly ChoreCreated Boss = new(BossId, Home, "Boss", "The big one");
    static readonly ChoreCreated Bonus = new(BonusId, Home, "Bonus", "Something extra", IsOptional: true);

    static readonly MemberJoinedHousehold AnnJoined = new(Ann, Home, Guid.NewGuid(), "Ann");
    static readonly MemberJoinedHousehold BobJoined = new(Bob, Home, Guid.NewGuid(), "Bob");

    static ChoreAssigned AssignedTo(Guid chore, params Guid[] members) => new(chore, Home, members, false);
    static ChoreAssigned AssignedToAll(Guid chore) => new(chore, Home, null, true);
    static ChoreCompleted Done(Guid chore, Guid by, DateTime at) => new(chore, Home, by, at);

    // Several completions of one chore, one hour apart, ending at `last`.
    static List<ChoreCompleted> Dones(Guid chore, Guid by, int times, DateTime last) =>
        Enumerable.Range(0, times).Select(i => Done(chore, by, last.AddHours(-i))).ToList();

    // ── Quests ───────────────────────────────────────────────────────────────

    static GetQuestsResponse Quests(IReadOnlyList<object> h) => QuestsView.Project(h, Now);

    [Fact]
    public void There_are_no_quests_before_a_chore_exists() =>
        FamilyQuest.Scenario("There are no quests before a chore exists")
            .Given()
            .ThenView("Quests", Quests, new { quests = Array.Empty<object>() });

    [Fact]
    public void Each_chore_is_a_quest_of_its_kind_with_its_xp() =>
        FamilyQuest.Scenario("Each chore is a quest of its kind, with its XP")
            .Given(Dishes, Laundry, Bins, Boss, Bonus)
            .ThenView("Quests", Quests, new
            {
                quests = new object[]
                {
                    new { choreId = DishesId, name = "Dishes", questType = "daily", xpReward = 10 },
                    new { choreId = LaundryId, name = "Laundry", questType = "weekly", xpReward = 25 },
                    new { choreId = BinsId, name = "Bins", questType = "weekly", xpReward = 20 },
                    new { choreId = BossId, name = "Boss", questType = "boss", xpReward = 50 },
                    new { choreId = BonusId, name = "Bonus", questType = "bonus", xpReward = 15 },
                },
            });

    [Fact]
    public void A_deleted_chore_is_not_a_quest() =>
        FamilyQuest.Scenario("A deleted chore is not a quest")
            .Given(Dishes, Laundry, new ChoreDeleted(LaundryId, Home, Ann))
            .ThenView("Quests", Quests, new { quests = new[] { new { choreId = DishesId } } });

    [Fact]
    public void A_quest_says_who_it_is_for() =>
        FamilyQuest.Scenario("A quest says who it is for")
            .Given(Dishes, Laundry, AssignedTo(DishesId, Ann), AssignedToAll(LaundryId))
            .ThenView("Quests", Quests, new
            {
                quests = new object[]
                {
                    new { choreId = DishesId, assignedTo = new[] { Ann }, assignedToAll = false },
                    new { choreId = LaundryId, assignedTo = (Guid[]?)null, assignedToAll = true },
                },
            });

    [Fact]
    public void A_quest_done_today_says_who_did_it() =>
        FamilyQuest.Scenario("A quest done today says who did it")
            .Given(Dishes, Done(DishesId, Ann, Now), Done(DishesId, Bob, Now.AddHours(-2)))
            .ThenView("Quests", Quests, new
            {
                quests = new[] { new { choreId = DishesId, completedToday = true, completedTodayBy = new[] { Ann, Bob } } },
            });

    [Fact]
    public void A_quest_done_yesterday_is_not_done_today() =>
        FamilyQuest.Scenario("A quest done yesterday is not done today")
            .Given(Dishes, Done(DishesId, Ann, Yesterday.AddHours(9)))
            .ThenView("Quests", Quests, new
            {
                quests = new[] { new { choreId = DishesId, completedToday = false, completedTodayBy = Array.Empty<object>() } },
            });

    // ── Victories ────────────────────────────────────────────────────────────

    static readonly DateTime Mar14 = new(2026, 3, 14, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar15 = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar16 = new(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Mar17 = new(2026, 3, 17, 9, 0, 0, DateTimeKind.Utc);

    static GetVictoriesResponse Victories(IReadOnlyList<object> h) => VictoriesView.Project(h, 10);

    [Fact]
    public void There_are_no_victories_before_anything_is_done() =>
        FamilyQuest.Scenario("There are no victories before anything is done")
            .Given(Dishes)
            .ThenView("Victories", Victories, new { victories = Array.Empty<object>() });

    [Fact]
    public void Victories_are_the_newest_completions_with_who_did_them() =>
        FamilyQuest.Scenario("Victories are the newest completions, with who did them")
            .Given(AnnJoined, BobJoined, Dishes, Laundry, Done(LaundryId, Ann, Mar16), Done(DishesId, Bob, Mar17))
            .ThenView("Victories", Victories, new
            {
                victories = new object[]
                {
                    new { choreId = DishesId, memberId = Bob, memberNickname = "Bob", choreName = "Dishes", completedAt = Mar17 },
                    new { choreId = LaundryId, memberId = Ann, memberNickname = "Ann", choreName = "Laundry", completedAt = Mar16 },
                },
            });

    [Fact]
    public void Only_the_newest_few_victories_are_shown() =>
        FamilyQuest.Scenario("Only the newest few victories are shown")
            .Given(AnnJoined, Dishes, Done(DishesId, Ann, Mar14), Done(DishesId, Ann, Mar15), Done(DishesId, Ann, Mar16))
            .ThenView("Victories", h => VictoriesView.Project(h, 2), new
            {
                victories = new object[]
                {
                    new { completedAt = Mar16 },
                    new { completedAt = Mar15 },
                },
            });

    [Fact]
    public void A_victory_shows_a_members_latest_name() =>
        FamilyQuest.Scenario("A victory shows a member's latest name")
            .Given(AnnJoined, new MemberNicknameChanged(Ann, Home, "Ann", "Annie"), Dishes, Done(DishesId, Ann, Mar17))
            .ThenView("Victories", Victories, new
            {
                victories = new[] { new { memberNickname = "Annie" } },
            });

    [Fact]
    public void A_victory_with_no_record_of_the_member_or_chore_shows_as_unknown() =>
        FamilyQuest.Scenario("A victory with no record of the member or chore shows as unknown")
            .Given(Done(DishesId, Bob, Mar17))
            .ThenView("Victories", Victories, new
            {
                victories = new[] { new { memberNickname = "Unknown", choreName = "Unknown chore" } },
            });

    // ── Party ────────────────────────────────────────────────────────────────

    static GetPartyResponse Party(IReadOnlyList<object> h) => PartyView.Project(h, Now);

    [Fact]
    public void There_is_no_party_before_anyone_joins() =>
        FamilyQuest.Scenario("There is no party before anyone joins")
            .Given()
            .ThenView("Party", Party, new { heroes = Array.Empty<object>() });

    [Fact]
    public void A_hero_has_xp_a_level_and_a_title_from_their_completions() =>
        FamilyQuest.Scenario("A hero has XP, a level and a title from their completions")
            .Given(AnnJoined, Boss, Dones(BossId, Ann, 8, Now.AddHours(-1)))
            .ThenView("Party", Party, new
            {
                heroes = new[]
                {
                    new { memberId = Ann, nickname = "Ann", xp = 400, level = 2, title = "Apprentice", totalCompletions = 8, completionsToday = 8 },
                },
            });

    [Fact]
    public void A_new_member_is_a_recruit_at_level_zero() =>
        FamilyQuest.Scenario("A new member is a recruit at level zero")
            .Given(AnnJoined, Dishes, Done(DishesId, Ann, Now))
            .ThenView("Party", Party, new
            {
                heroes = new[] { new { memberId = Ann, xp = 10, level = 0, title = "Recruit", totalCompletions = 1, completionsToday = 1 } },
            });

    [Fact]
    public void A_removed_member_is_not_a_hero() =>
        FamilyQuest.Scenario("A removed member is not a hero")
            .Given(AnnJoined, BobJoined, new MemberRemoved(Home, Bob, Ann, "Bob"))
            .ThenView("Party", Party, new
            {
                heroes = new[] { new { memberId = Ann, nickname = "Ann", status = (string?)null } },
            });

    [Fact]
    public void A_hero_shows_the_latest_name_and_status() =>
        FamilyQuest.Scenario("A hero shows the latest name and status")
            .Given(AnnJoined, new MemberNicknameChanged(Ann, Home, "Ann", "Annie"), new MemberStatusChanged(Ann, Home, "On holiday"))
            .ThenView("Party", Party, new
            {
                heroes = new[] { new { memberId = Ann, nickname = "Annie", status = "On holiday" } },
            });

    [Fact]
    public void A_completion_of_an_unknown_chore_is_worth_ten_xp() =>
        FamilyQuest.Scenario("A completion of an unknown chore is worth ten XP")
            .Given(AnnJoined, Done(DishesId, Ann, Now))
            .ThenView("Party", Party, new
            {
                heroes = new[] { new { memberId = Ann, xp = 10, level = 0 } },
            });

    // ── XP ───────────────────────────────────────────────────────────────────

    static GetXpResponse Xp(IReadOnlyList<object> h) => XpView.Project(h);

    [Fact]
    public void Before_anything_is_done_the_first_reward_is_next() =>
        FamilyQuest.Scenario("Before anything is done, the first reward is next")
            .Given()
            .ThenView("XP", Xp, new
            {
                totalXp = 0,
                currentReward = (object?)null,
                nextReward = new { xpRequired = 500, reward = "Pizza Night" },
                xpToNext = 500,
                progressPercent = 0,
            });

    [Fact]
    public void Each_completion_is_worth_its_chores_xp() =>
        FamilyQuest.Scenario("Each completion is worth its chore's XP")
            .Given(AnnJoined, Dishes, Laundry, Bins, Boss, Bonus,
                Done(DishesId, Ann, Now), Done(LaundryId, Ann, Now), Done(BinsId, Ann, Now), Done(BossId, Ann, Now), Done(BonusId, Ann, Now))
            .ThenView("XP", Xp, new
            {
                totalXp = 120,
                currentReward = (object?)null,
                nextReward = new { xpRequired = 500, reward = "Pizza Night" },
                xpToNext = 380,
                progressPercent = 24,
            });

    [Fact]
    public void Reaching_a_reward_gives_it_and_the_next_one_to_aim_for() =>
        FamilyQuest.Scenario("Reaching a reward gives it, and the next one to aim for")
            .Given(AnnJoined, Boss, Dones(BossId, Ann, 10, Now))
            .ThenView("XP", Xp, new
            {
                totalXp = 500,
                currentReward = new { xpRequired = 500, reward = "Pizza Night" },
                nextReward = new { xpRequired = 1500, reward = "Movie Night" },
                xpToNext = 1000,
                progressPercent = 0,
            });
}
