using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Infrastructure.Automations;

/// <summary>
/// Records activities as immutable events when things happen in a household.
/// This captures nicknames and chore names at the time of the action, so they
/// don't change when members update their profiles later.
/// </summary>
public class ActivityRecorder :
    INotificationHandler<ChoreCompleted>,
    INotificationHandler<MemberJoinedHousehold>,
    INotificationHandler<ChoreAssigned>,
    INotificationHandler<MemberNicknameChanged>,
    INotificationHandler<MemberStatusChanged>,
    INotificationHandler<ChoreCreated>
{
    private readonly IEventStore _store;

    public ActivityRecorder(IEventStore store)
    {
        // Use raw store to avoid circular publishing
        _store = store is PublishingEventStore pub ? pub.Inner : store;
    }

    private static string ActivityStreamId(Guid householdId) => $"activities-{householdId}";

    private async Task<Dictionary<Guid, string>> GetMemberNicknames(Guid householdId)
    {
        var events = await _store.FetchEventsAsync(HouseholdAggregate.StreamId(householdId));
        var nicknames = new Dictionary<Guid, string>();
        
        foreach (var evt in events)
        {
            if (evt is MemberJoinedHousehold joined)
                nicknames[joined.MemberId] = joined.Nickname;
            else if (evt is MemberNicknameChanged changed)
                nicknames[changed.MemberId] = changed.NewNickname;
        }
        
        return nicknames;
    }

    private async Task<Dictionary<Guid, string>> GetChoreNames(Guid householdId)
    {
        var events = await _store.FetchEventsAsync(ChoreAggregate.StreamId(householdId));
        return events
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);
    }

    public async Task Handle(ChoreCompleted notification, CancellationToken cancellationToken)
    {
        var nicknames = await GetMemberNicknames(notification.HouseholdId);
        var choreNames = await GetChoreNames(notification.HouseholdId);
        
        var memberName = nicknames.GetValueOrDefault(notification.CompletedByMemberId, "Someone");
        var choreName = choreNames.GetValueOrDefault(notification.ChoreId, "a chore");

        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "completion",
            Text: $"{memberName} completed {choreName}",
            ActorId: notification.CompletedByMemberId,
            ActorNickname: memberName,
            ChoreId: notification.ChoreId,
            ChoreName: choreName,
            ExtraJson: null
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    public async Task Handle(MemberJoinedHousehold notification, CancellationToken cancellationToken)
    {
        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "member_joined",
            Text: $"{notification.Nickname} joined the household",
            ActorId: notification.MemberId,
            ActorNickname: notification.Nickname,
            ChoreId: null,
            ChoreName: null,
            ExtraJson: null
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    public async Task Handle(ChoreAssigned notification, CancellationToken cancellationToken)
    {
        var nicknames = await GetMemberNicknames(notification.HouseholdId);
        var choreNames = await GetChoreNames(notification.HouseholdId);
        
        var choreName = choreNames.GetValueOrDefault(notification.ChoreId, "a chore");
        var assignerName = notification.AssignedByMemberId.HasValue 
            ? nicknames.GetValueOrDefault(notification.AssignedByMemberId.Value, null)
            : null;

        string text;

        if (notification.AssignToAll)
        {
            text = assignerName != null
                ? $"{assignerName} assigned {choreName} to everyone"
                : $"{choreName} was assigned to everyone";
        }
        else if (notification.AssignedToMemberIds?.Length == 1 &&
                 notification.AssignedByMemberId.HasValue &&
                 notification.AssignedToMemberIds[0] == notification.AssignedByMemberId.Value)
        {
            // Self-claim
            text = $"{assignerName} claimed {choreName}";
        }
        else
        {
            var assigneeNames = notification.AssignedToMemberIds?
                .Select(id => nicknames.GetValueOrDefault(id, "someone"))
                .ToArray() ?? Array.Empty<string>();
            var assignees = assigneeNames.Length > 0 ? string.Join(", ", assigneeNames) : "unknown";
            
            text = assignerName != null
                ? $"{assignerName} assigned {choreName} to {assignees}"
                : $"{choreName} was assigned to {assignees}";
        }

        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "chore_assigned",
            Text: text,
            ActorId: notification.AssignedByMemberId,
            ActorNickname: assignerName,
            ChoreId: notification.ChoreId,
            ChoreName: choreName,
            ExtraJson: null
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    public async Task Handle(MemberNicknameChanged notification, CancellationToken cancellationToken)
    {
        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "nickname_changed",
            Text: $"{notification.OldNickname} is now {notification.NewNickname}",
            ActorId: notification.MemberId,
            ActorNickname: notification.NewNickname,
            ChoreId: null,
            ChoreName: null,
            ExtraJson: System.Text.Json.JsonSerializer.Serialize(new { notification.OldNickname, notification.NewNickname })
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    public async Task Handle(MemberStatusChanged notification, CancellationToken cancellationToken)
    {
        var nicknames = await GetMemberNicknames(notification.HouseholdId);
        var memberName = nicknames.GetValueOrDefault(notification.MemberId, "Someone");

        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "status_changed",
            Text: $"{memberName}: {notification.Status}",
            ActorId: notification.MemberId,
            ActorNickname: memberName,
            ChoreId: null,
            ChoreName: null,
            ExtraJson: System.Text.Json.JsonSerializer.Serialize(new { notification.Status })
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    public async Task Handle(ChoreCreated notification, CancellationToken cancellationToken)
    {
        var activity = new ActivityRecorded(
            ActivityId: Guid.NewGuid(),
            HouseholdId: notification.HouseholdId,
            Type: "chore_created",
            Text: $"New chore: {notification.DisplayName}",
            ActorId: null,
            ActorNickname: null,
            ChoreId: notification.ChoreId,
            ChoreName: notification.DisplayName,
            ExtraJson: null
        );

        await AppendActivity(notification.HouseholdId, activity);
    }

    private async Task AppendActivity(Guid householdId, ActivityRecorded activity)
    {
        var streamId = ActivityStreamId(householdId);
        
        try
        {
            await _store.AppendToStreamAsync(streamId, new[] { activity }, ExpectedVersion.Any);
        }
        catch
        {
            // First activity - start the stream
            await _store.StartStreamAsync(streamId, new[] { activity });
        }
    }
}
