using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using ChoreMonkey.Core.Infrastructure.ReadModels;
using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Infrastructure.Automations;

/// <summary>
/// Records activities to the read model when things happen in a household.
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
    private readonly IActivityReadModel _activityReadModel;

    public ActivityRecorder(IEventStore store, IActivityReadModel activityReadModel)
    {
        // Use raw store to avoid circular publishing
        _store = store is PublishingEventStore pub ? pub.Inner : store;
        _activityReadModel = activityReadModel;
    }

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

        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "completion",
            TimestampUtc: notification.CompletedAt.ToString("O"),
            Text: $"{memberName} completed {choreName}",
            ChoreId: notification.ChoreId,
            ChoreName: choreName,
            ActorId: notification.CompletedByMemberId,
            ActorNickname: memberName
        ));
    }

    public async Task Handle(MemberJoinedHousehold notification, CancellationToken cancellationToken)
    {
        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "member_joined",
            TimestampUtc: notification.TimestampUtc,
            Text: $"{notification.Nickname} joined the household",
            ActorId: notification.MemberId,
            ActorNickname: notification.Nickname
        ));
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

        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "chore_assigned",
            TimestampUtc: notification.TimestampUtc,
            Text: text,
            ChoreId: notification.ChoreId,
            ChoreName: choreName,
            ActorId: notification.AssignedByMemberId,
            ActorNickname: assignerName
        ));
    }

    public async Task Handle(MemberNicknameChanged notification, CancellationToken cancellationToken)
    {
        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "nickname_changed",
            TimestampUtc: notification.TimestampUtc,
            Text: $"{notification.OldNickname} is now {notification.NewNickname}",
            ActorId: notification.MemberId,
            ActorNickname: notification.NewNickname
        ));
    }

    public async Task Handle(MemberStatusChanged notification, CancellationToken cancellationToken)
    {
        var nicknames = await GetMemberNicknames(notification.HouseholdId);
        var memberName = nicknames.GetValueOrDefault(notification.MemberId, "Someone");

        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "status_changed",
            TimestampUtc: notification.TimestampUtc,
            Text: $"{memberName}: {notification.Status}",
            ActorId: notification.MemberId,
            ActorNickname: memberName
        ));
    }

    public async Task Handle(ChoreCreated notification, CancellationToken cancellationToken)
    {
        await _activityReadModel.AppendActivityAsync(notification.HouseholdId, new ActivityItem(
            Type: "chore_created",
            TimestampUtc: notification.TimestampUtc,
            Text: $"New chore: {notification.DisplayName}",
            ChoreId: notification.ChoreId,
            ChoreName: notification.DisplayName
        ));
    }
}
