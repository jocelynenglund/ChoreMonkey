using ChoreMonkey.Core.Feature.Chores.Commands.AcknowledgeMissed;
using ChoreMonkey.Core.Feature.Chores.Commands.AddChore;
using ChoreMonkey.Core.Feature.Chores.Commands.AssignChore;
using ChoreMonkey.Core.Feature.Chores.Commands.CompleteChore;
using ChoreMonkey.Core.Feature.Chores.Commands.DeleteChore;
using ChoreMonkey.Core.Feature.Chores.Commands.PauseChore;
using ChoreMonkey.Core.Feature.Chores.Commands.RemoveChorePause;
using ChoreMonkey.Core.Feature.Chores.Commands.UpdateChore;
using ChoreMonkey.Core.Security;
using static ItsyBIT.EventStore.Testing.Gwt;

namespace ChoreMonkey.Gwt.Tests;

/// <summary>The Chores chapter, commands: one test per scenario, the happy case first, then each refusal or branch.</summary>
public class ChoresSpecs
{
    static readonly HistoryChapter Chores = new("Chores");

    static readonly Guid Home = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid DishesId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    static readonly Guid LaundryId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    static readonly Guid Ann = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    static readonly Guid Bob = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    static readonly Guid PauseA = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    static readonly Guid PauseB = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    static readonly DateTime Now = new(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);

    // The admin PIN is 1234; hashing is deliberately slow, so do it once.
    const int AdminPin = 1234;
    static readonly string AdminPinHash = PinHasher.HashPin(AdminPin);

    static readonly HouseholdCreated Household = new(Home, "Home", AdminPinHash);
    static readonly ChoreCreated Dishes = new(DishesId, Home, "Dishes", "Wash up", new ChoreFrequency("daily"));
    static readonly ChoreDeleted DishesDeleted = new(DishesId, Home, Guid.Empty);
    static readonly ChorePaused Paused = new(PauseA, DishesId, Home, new DateTime(2026, 3, 1), new DateTime(2026, 3, 7), "Holiday");

    // ── Add Chore ────────────────────────────────────────────────────────────

    static AddChoreCommand Add(ChoreFrequency? frequency = null) =>
        new(Home, DishesId, "Dishes", "Wash up", frequency, IsOptional: false, StartDate: null, IsRequired: true, MissedDeduction: 10m);

    [Fact]
    public void A_chore_is_added() =>
        Chores.Scenario("A chore is added")
            .Given()
            .When("Add Chore", Add(new ChoreFrequency("weekly", ["monday", "thursday"])), AddChoreSlice.Decide)
            .Then(Event<ChoreCreated>(new
            {
                choreId = DishesId, householdId = Home, displayName = "Dishes", description = "Wash up",
                frequency = new { type = "weekly", days = new[] { "monday", "thursday" } },
                isOptional = false, isRequired = true, missedDeduction = 10m,
            }));

    [Fact]
    public void A_chore_with_no_frequency_is_a_one_time_chore() =>
        Chores.Scenario("A chore with no frequency is a one-time chore")
            .Given()
            .When("Add Chore", Add(), AddChoreSlice.Decide)
            .Then(Event<ChoreCreated>(new { choreId = DishesId, frequency = new { type = "once" } }));

    [Fact]
    public void An_optional_chore_keeps_its_start_date_and_deduction() =>
        Chores.Scenario("An optional chore keeps its start date and deduction")
            .Given()
            .When("Add Chore", Add(new ChoreFrequency("daily")) with { IsOptional = true, IsRequired = false, StartDate = new DateTime(2026, 4, 1), MissedDeduction = 25m }, AddChoreSlice.Decide)
            .Then(Event<ChoreCreated>(new { choreId = DishesId, isOptional = true, isRequired = false, missedDeduction = 25m, startDate = new DateTime(2026, 4, 1) }));

    [Fact]
    public void Adding_a_chore_with_an_id_already_in_use_records_it_again() =>
        Chores.Scenario("Adding a chore with an id already in use records it again")
            .Given(Dishes)
            .When("Add Chore", Add(new ChoreFrequency("daily")), AddChoreSlice.Decide)
            .Then(Event<ChoreCreated>(new { choreId = DishesId, displayName = "Dishes" }));

    // ── Update Chore ─────────────────────────────────────────────────────────

    static UpdateChoreCommand Update(Guid choreId) =>
        new(Home, choreId, "Dishes and pans", "Wash up properly", new ChoreFrequency("weekly"), IsOptional: true, StartDate: null, IsRequired: false, MissedDeduction: 5m);

    [Fact]
    public void A_chore_is_updated() =>
        Chores.Scenario("A chore is updated")
            .Given(Dishes)
            .When("Update Chore", Update(DishesId), UpdateChoreSlice.Decide)
            .Then(Event<ChoreUpdated>(new
            {
                choreId = DishesId, householdId = Home, displayName = "Dishes and pans", description = "Wash up properly",
                frequency = new { type = "weekly" }, isOptional = true, isRequired = false, missedDeduction = 5m,
            }));

    [Fact]
    public void An_unknown_chore_cant_be_updated() =>
        Chores.Scenario("An unknown chore can't be updated")
            .Given()
            .When("Update Chore", Update(DishesId), UpdateChoreSlice.Decide)
            .ThenNothing("Chore not found.", "NotFound");

    [Fact]
    public void A_chore_made_by_another_id_doesnt_count() =>
        Chores.Scenario("Another chore existing doesn't make this one updatable")
            .Given(Dishes)
            .When("Update Chore", Update(LaundryId), UpdateChoreSlice.Decide)
            .ThenNothing("Chore not found.", "NotFound");

    [Fact]
    public void A_deleted_chore_cant_be_updated() =>
        Chores.Scenario("A deleted chore can't be updated")
            .Given(Dishes, DishesDeleted)
            .When("Update Chore", Update(DishesId), UpdateChoreSlice.Decide)
            .ThenNothing("Chore not found.", "NotFound");

    // ── Assign Chore ─────────────────────────────────────────────────────────

    [Fact]
    public void A_chore_is_assigned_to_members() =>
        Chores.Scenario("A chore is assigned to members")
            .Given(Dishes)
            .When("Assign Chore", new AssignChoreCommand(Home, DishesId, [Ann, Bob], false), AssignChoreSlice.Decide)
            .Then(Event<ChoreAssigned>(new { choreId = DishesId, householdId = Home, assignedToMemberIds = new[] { Ann, Bob }, assignToAll = false }));

    [Fact]
    public void A_chore_is_assigned_to_everyone() =>
        Chores.Scenario("A chore is assigned to everyone")
            .Given(Dishes)
            .When("Assign Chore", new AssignChoreCommand(Home, DishesId, null, true), AssignChoreSlice.Decide)
            .Then(Event<ChoreAssigned>(new { choreId = DishesId, assignToAll = true }));

    [Fact]
    public void Assigning_does_not_check_that_the_chore_exists() =>
        Chores.Scenario("Assigning doesn't check that the chore exists")
            .Given()
            .When("Assign Chore", new AssignChoreCommand(Home, LaundryId, [Ann], false), AssignChoreSlice.Decide)
            .Then(Event<ChoreAssigned>(new { choreId = LaundryId, assignedToMemberIds = new[] { Ann } }));

    // ── Complete Chore ───────────────────────────────────────────────────────

    static Decision Complete(IReadOnlyList<object> history, CompleteChoreCommand cmd) => CompleteChoreSlice.Decide(history, cmd, Now);

    [Fact]
    public void A_member_who_is_not_assigned_is_assigned_then_completes() =>
        Chores.Scenario("A member who isn't assigned is assigned first, then completes")
            .Given()
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Ann), Complete)
            .Then(
                Event<ChoreAssigned>(new { choreId = DishesId, householdId = Home, assignedToMemberIds = new[] { Ann }, assignToAll = false }),
                Event<ChoreCompleted>(new { choreId = DishesId, householdId = Home, completedByMemberId = Ann, completedAt = Now }));

    [Fact]
    public void An_assigned_member_just_completes() =>
        Chores.Scenario("An assigned member just completes")
            .Given(Dishes, new ChoreAssigned(DishesId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Ann), Complete)
            .Then(Event<ChoreCompleted>(new { choreId = DishesId, completedByMemberId = Ann, completedAt = Now }));

    [Fact]
    public void When_the_chore_is_for_everyone_anyone_just_completes() =>
        Chores.Scenario("When the chore is for everyone, anyone just completes")
            .Given(Dishes, new ChoreAssigned(DishesId, Home, null, true))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Bob), Complete)
            .Then(Event<ChoreCompleted>(new { choreId = DishesId, completedByMemberId = Bob }));

    [Fact]
    public void A_member_who_is_not_assigned_joins_the_assignees() =>
        Chores.Scenario("A member who isn't assigned joins the assignees")
            .Given(Dishes, new ChoreAssigned(DishesId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Bob), Complete)
            .Then(
                Event<ChoreAssigned>(new { choreId = DishesId, assignedToMemberIds = new[] { Ann, Bob }, assignToAll = false }),
                Event<ChoreCompleted>(new { choreId = DishesId, completedByMemberId = Bob }));

    [Fact]
    public void Only_the_latest_assignment_counts() =>
        Chores.Scenario("Only the latest assignment counts")
            .Given(Dishes, new ChoreAssigned(DishesId, Home, [Ann, Bob], false), new ChoreAssigned(DishesId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Bob), Complete)
            .Then(
                Event<ChoreAssigned>(new { assignedToMemberIds = new[] { Ann, Bob } }),
                Event<ChoreCompleted>(new { completedByMemberId = Bob }));

    [Fact]
    public void Another_chores_assignment_doesnt_count() =>
        Chores.Scenario("Another chore's assignment doesn't count")
            .Given(Dishes, new ChoreAssigned(LaundryId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Ann), Complete)
            .Then(
                Event<ChoreAssigned>(new { choreId = DishesId, assignedToMemberIds = new[] { Ann } }),
                Event<ChoreCompleted>(new { choreId = DishesId }));

    [Fact]
    public void A_completion_can_be_dated() =>
        Chores.Scenario("A completion can be dated")
            .Given(Dishes, new ChoreAssigned(DishesId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Ann, new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Utc)), Complete)
            .Then(Event<ChoreCompleted>(new { completedAt = new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Utc) }));

    [Fact]
    public void A_deleted_chore_can_still_be_completed() =>
        Chores.Scenario("A deleted chore can still be completed")
            .Given(Dishes, DishesDeleted, new ChoreAssigned(DishesId, Home, [Ann], false))
            .When("Complete Chore", new CompleteChoreCommand(Home, DishesId, Ann), Complete)
            .Then(Event<ChoreCompleted>(new { choreId = DishesId, completedByMemberId = Ann }));

    // ── Delete Chore ─────────────────────────────────────────────────────────

    static DeleteChoreCommand Delete(int pin = AdminPin) => new(Home, DishesId, pin);

    [Fact]
    public void A_chore_is_deleted() =>
        Chores.Scenario("A chore is deleted")
            .Given(Household, Dishes)
            .When("Delete Chore", Delete(), DeleteChoreSlice.Decide)
            .Then(Event<ChoreDeleted>(new { choreId = DishesId, householdId = Home, deletedByMemberId = Guid.Empty }));

    [Fact]
    public void Only_the_admin_can_delete_a_chore() =>
        Chores.Scenario("Only the admin can delete a chore")
            .Given(Household, Dishes)
            .When("Delete Chore", Delete(9999), DeleteChoreSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void There_is_no_deleting_without_a_household() =>
        Chores.Scenario("There's no deleting without a household")
            .Given(Dishes)
            .When("Delete Chore", Delete(), DeleteChoreSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void The_changed_admin_pin_is_the_one_that_counts() =>
        Chores.Scenario("The changed admin PIN is the one that counts")
            .Given(Household, new AdminPinChanged(Home, PinHasher.HashPin(4321)), Dishes)
            .When("Delete Chore", Delete(4321), DeleteChoreSlice.Decide)
            .Then(Event<ChoreDeleted>(new { choreId = DishesId }));

    [Fact]
    public void The_old_admin_pin_stops_working_after_it_is_changed() =>
        Chores.Scenario("The old admin PIN stops working after it is changed")
            .Given(Household, new AdminPinChanged(Home, PinHasher.HashPin(4321)), Dishes)
            .When("Delete Chore", Delete(AdminPin), DeleteChoreSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void An_unknown_chore_cant_be_deleted() =>
        Chores.Scenario("An unknown chore can't be deleted")
            .Given(Household)
            .When("Delete Chore", Delete(), DeleteChoreSlice.Decide)
            .ThenNothing("Chore not found", "NotFound");

    [Fact]
    public void Deleting_a_deleted_chore_records_nothing() =>
        Chores.Scenario("Deleting a deleted chore records nothing")
            .Given(Household, Dishes, DishesDeleted)
            .When("Delete Chore", Delete(), DeleteChoreSlice.Decide)
            .ThenNothing();

    // ── Pause Chore ──────────────────────────────────────────────────────────

    static PauseChoreCommand Pause(Guid? pauseId = null, DateTime? start = null, DateTime? end = null, string? reason = "Holiday", int pin = AdminPin, Guid? choreId = null) =>
        new(Home, choreId ?? DishesId, pauseId ?? PauseA, start ?? new DateTime(2026, 3, 1), end ?? new DateTime(2026, 3, 7), reason, pin);

    [Fact]
    public void A_chore_is_paused() =>
        Chores.Scenario("A chore is paused")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new
            {
                pauseId = PauseA, choreId = DishesId, householdId = Home,
                pauseStart = new DateTime(2026, 3, 1), pauseEnd = new DateTime(2026, 3, 7), reason = "Holiday",
            }));

    [Fact]
    public void A_pause_covers_whole_days() =>
        Chores.Scenario("A pause covers whole days")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(start: new DateTime(2026, 3, 1, 14, 30, 0), end: new DateTime(2026, 3, 1, 8, 0, 0)), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new { pauseStart = new DateTime(2026, 3, 1), pauseEnd = new DateTime(2026, 3, 1) }));

    [Fact]
    public void The_reason_is_trimmed() =>
        Chores.Scenario("The reason is trimmed")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(reason: "  Away  "), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new { reason = "Away" }));

    [Fact]
    public void A_blank_reason_is_left_out() =>
        Chores.Scenario("A blank reason is left out")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(reason: "   "), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new { choreId = DishesId, reason = (string?)null }));

    [Fact]
    public void An_empty_pause_id_gets_a_new_one() =>
        Chores.Scenario("An empty pause id gets a new one")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(pauseId: Guid.Empty), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new { pauseId = Where(j => j.GetString() != Guid.Empty.ToString() && Guid.TryParse(j.GetString(), out _)), choreId = DishesId }));

    [Fact]
    public void Pauses_may_overlap() =>
        Chores.Scenario("Pauses may overlap")
            .Given(Household, Dishes, Paused)
            .When("Pause Chore", Pause(pauseId: PauseB, start: new DateTime(2026, 3, 5), end: new DateTime(2026, 3, 9)), PauseChoreSlice.Decide)
            .Then(Event<ChorePaused>(new { pauseId = PauseB }));

    [Fact]
    public void Only_the_admin_can_pause_a_chore() =>
        Chores.Scenario("Only the admin can pause a chore")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(pin: 9999), PauseChoreSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void There_is_no_pausing_without_a_household() =>
        Chores.Scenario("There's no pausing without a household")
            .Given(Dishes)
            .When("Pause Chore", Pause(), PauseChoreSlice.Decide)
            .ThenNothing("Household not found", "Forbidden");

    [Fact]
    public void A_pause_cant_end_before_it_starts() =>
        Chores.Scenario("A pause can't end before it starts")
            .Given(Household, Dishes)
            .When("Pause Chore", Pause(start: new DateTime(2026, 3, 8), end: new DateTime(2026, 3, 7)), PauseChoreSlice.Decide)
            .ThenNothing("Pause end date must be on or after the start date", "Invalid");

    [Fact]
    public void An_unknown_chore_cant_be_paused() =>
        Chores.Scenario("An unknown chore can't be paused")
            .Given(Household)
            .When("Pause Chore", Pause(), PauseChoreSlice.Decide)
            .ThenNothing("Chore not found", "Invalid");

    [Fact]
    public void A_deleted_chore_cant_be_paused() =>
        Chores.Scenario("A deleted chore can't be paused")
            .Given(Household, Dishes, DishesDeleted)
            .When("Pause Chore", Pause(), PauseChoreSlice.Decide)
            .ThenNothing("Chore not found", "Invalid");

    // ── Remove Chore Pause ───────────────────────────────────────────────────

    static RemoveChorePauseCommand RemovePause(Guid? pauseId = null, int pin = AdminPin, Guid? choreId = null) =>
        new(Home, choreId ?? DishesId, pauseId ?? PauseA, pin);

    [Fact]
    public void A_pause_is_removed() =>
        Chores.Scenario("A pause is removed")
            .Given(Household, Dishes, Paused)
            .When("Remove Chore Pause", RemovePause(), RemoveChorePauseSlice.Decide)
            .Then(Event<ChorePauseRemoved>(new { pauseId = PauseA, choreId = DishesId, householdId = Home }));

    [Fact]
    public void Only_the_admin_can_remove_a_pause() =>
        Chores.Scenario("Only the admin can remove a pause")
            .Given(Household, Dishes, Paused)
            .When("Remove Chore Pause", RemovePause(pin: 9999), RemoveChorePauseSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void There_is_no_removing_a_pause_without_a_household() =>
        Chores.Scenario("There's no removing a pause without a household")
            .Given(Dishes, Paused)
            .When("Remove Chore Pause", RemovePause(), RemoveChorePauseSlice.Decide)
            .ThenNothing("Admin access required", "Forbidden");

    [Fact]
    public void An_unknown_pause_cant_be_removed() =>
        Chores.Scenario("An unknown pause can't be removed")
            .Given(Household, Dishes, Paused)
            .When("Remove Chore Pause", RemovePause(pauseId: PauseB), RemoveChorePauseSlice.Decide)
            .ThenNothing("Pause not found", "NotFound");

    [Fact]
    public void A_removed_pause_cant_be_removed_again() =>
        Chores.Scenario("A removed pause can't be removed again")
            .Given(Household, Dishes, Paused, new ChorePauseRemoved(PauseA, DishesId, Home))
            .When("Remove Chore Pause", RemovePause(), RemoveChorePauseSlice.Decide)
            .ThenNothing("Pause not found", "NotFound");

    [Fact]
    public void A_pause_belongs_to_its_chore() =>
        Chores.Scenario("A pause belongs to its chore")
            .Given(Household, Dishes, Paused)
            .When("Remove Chore Pause", RemovePause(choreId: LaundryId), RemoveChorePauseSlice.Decide)
            .ThenNothing("Pause not found", "NotFound");

    // ── Acknowledge Missed ───────────────────────────────────────────────────

    [Fact]
    public void A_missed_chore_is_acknowledged() =>
        Chores.Scenario("A missed chore is acknowledged")
            .Given()
            .When("Acknowledge Missed", new AcknowledgeMissedCommand(Home, DishesId, Ann, "2026-03-17"), AcknowledgeMissedSlice.Decide)
            .Then(Event<ChoreMissedAcknowledged>(new { choreId = DishesId, memberId = Ann, period = "2026-03-17" }));

    [Fact]
    public void A_weekly_miss_is_acknowledged_by_week() =>
        Chores.Scenario("A weekly miss is acknowledged by week")
            .Given(Dishes)
            .When("Acknowledge Missed", new AcknowledgeMissedCommand(Home, DishesId, Ann, "2026-W11"), AcknowledgeMissedSlice.Decide)
            .Then(Event<ChoreMissedAcknowledged>(new { period = "2026-W11" }));

    [Fact]
    public void Acknowledging_twice_records_it_twice() =>
        Chores.Scenario("Acknowledging twice records it twice")
            .Given(Dishes, new ChoreMissedAcknowledged(DishesId, Ann, "2026-03-17"))
            .When("Acknowledge Missed", new AcknowledgeMissedCommand(Home, DishesId, Ann, "2026-03-17"), AcknowledgeMissedSlice.Decide)
            .Then(Event<ChoreMissedAcknowledged>(new { choreId = DishesId, memberId = Ann, period = "2026-03-17" }));
}
