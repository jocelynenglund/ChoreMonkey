using System.Text.Json;
using ChoreMonkey.Events;
using ChoreMonkey.Core.Domain;
using FileEventStore;
using Microsoft.Extensions.Options;

namespace ChoreMonkey.Core.Infrastructure.ReadModels;

public record ActivityItem(
    string Type,
    string TimestampUtc,
    string Text,
    Guid? ChoreId = null,
    string? ChoreName = null,
    Guid? ActorId = null,
    string? ActorNickname = null
);

public record ActivityReadModelData(
    Dictionary<string, long> LastReplayedPositions,
    List<ActivityItem> Items
);

public class ActivityReadModelOptions
{
    public string BasePath { get; set; } = "events";
}

public interface IActivityReadModel
{
    Task<List<ActivityItem>> GetActivitiesAsync(Guid householdId, int? days = null, int? limit = null);
    Task AppendActivityAsync(Guid householdId, ActivityItem activity);
    Task RebuildAsync(Guid householdId);
}

public class ActivityReadModel : IActivityReadModel
{
    private readonly IEventStore _store;
    private readonly string _basePath;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Per-household locks to prevent concurrent file access
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    
    private static SemaphoreSlim GetLock(Guid householdId) => 
        _locks.GetOrAdd(householdId, _ => new SemaphoreSlim(1, 1));

    public ActivityReadModel(IEventStore store, IOptions<ActivityReadModelOptions> options)
    {
        _store = store;
        _basePath = options.Value.BasePath;
    }

    private string GetFilePath(Guid householdId)
    {
        var activitiesPath = Path.Combine(_basePath, "activities");
        Directory.CreateDirectory(activitiesPath);
        return Path.Combine(activitiesPath, $"{householdId}.json");
    }

    public async Task<List<ActivityItem>> GetActivitiesAsync(Guid householdId, int? days = null, int? limit = null)
    {
        var filePath = GetFilePath(householdId);
        
        if (!File.Exists(filePath))
        {
            // Auto-rebuild if file doesn't exist
            await RebuildAsync(householdId);
        }

        if (!File.Exists(filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<ActivityReadModelData>(json, JsonOptions);
        
        if (data?.Items == null) return [];

        var cutoff = days.HasValue 
            ? DateTime.UtcNow.AddDays(-days.Value) 
            : DateTime.MinValue;

        return data.Items
            .Where(a => DateTime.TryParse(a.TimestampUtc, out var ts) && ts >= cutoff)
            .OrderByDescending(a => DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.MinValue)
            .Take(limit ?? 50)
            .ToList();
    }

    public async Task AppendActivityAsync(Guid householdId, ActivityItem activity)
    {
        var filePath = GetFilePath(householdId);
        var lockObj = GetLock(householdId);
        
        await lockObj.WaitAsync();
        try
        {
            ActivityReadModelData data;

            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                data = JsonSerializer.Deserialize<ActivityReadModelData>(json, JsonOptions) 
                    ?? new ActivityReadModelData([], []);
            }
            else
            {
                data = new ActivityReadModelData([], []);
            }

            // Append new activity
            var items = data.Items.ToList();
            items.Insert(0, activity); // Most recent first
            
            // Keep last 500 items max to prevent unbounded growth
            if (items.Count > 500)
            {
                items = items.Take(500).ToList();
            }

            var newData = data with { Items = items };
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(newData, JsonOptions));
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task RebuildAsync(Guid householdId)
    {
        var lockObj = GetLock(householdId);
        await lockObj.WaitAsync();
        try
        {
            await RebuildInternalAsync(householdId);
        }
        finally
        {
            lockObj.Release();
        }
    }

    private async Task RebuildInternalAsync(Guid householdId)
    {
        var householdStreamId = HouseholdAggregate.StreamId(householdId);
        var choreStreamId = ChoreAggregate.StreamId(householdId);

        var householdEvents = await _store.FetchEventsAsync(householdStreamId);
        var choreEvents = await _store.FetchEventsAsync(choreStreamId);

        // Build lookup tables
        var choreNames = choreEvents
            .OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        // Track nicknames at each point in time
        var memberNicknames = new Dictionary<Guid, string>();
        
        var activities = new List<ActivityItem>();

        // Process household events
        foreach (var evt in householdEvents)
        {
            switch (evt)
            {
                case MemberJoinedHousehold joined:
                    memberNicknames[joined.MemberId] = joined.Nickname;
                    activities.Add(new ActivityItem(
                        "member_joined",
                        joined.TimestampUtc,
                        $"{joined.Nickname} joined the household",
                        null, null,
                        joined.MemberId,
                        joined.Nickname
                    ));
                    break;

                case MemberNicknameChanged renamed:
                    var oldName = memberNicknames.GetValueOrDefault(renamed.MemberId, "Someone");
                    memberNicknames[renamed.MemberId] = renamed.NewNickname;
                    activities.Add(new ActivityItem(
                        "nickname_changed",
                        renamed.TimestampUtc,
                        $"{oldName} is now {renamed.NewNickname}",
                        null, null,
                        renamed.MemberId,
                        renamed.NewNickname
                    ));
                    break;

                case MemberStatusChanged status:
                    var statusNickname = memberNicknames.GetValueOrDefault(status.MemberId, "Someone");
                    activities.Add(new ActivityItem(
                        "status_changed",
                        status.TimestampUtc,
                        $"{statusNickname}: {status.Status}",
                        null, null,
                        status.MemberId,
                        statusNickname
                    ));
                    break;
            }
        }

        // Process chore events
        foreach (var evt in choreEvents)
        {
            switch (evt)
            {
                case ChoreCreated created:
                    activities.Add(new ActivityItem(
                        "chore_created",
                        created.TimestampUtc,
                        $"New chore: {created.DisplayName}",
                        created.ChoreId,
                        created.DisplayName
                    ));
                    break;

                case ChoreAssigned assigned:
                    var assignerNickname = assigned.AssignedByMemberId.HasValue
                        ? memberNicknames.GetValueOrDefault(assigned.AssignedByMemberId.Value, "Someone")
                        : null;
                    var choreName = choreNames.GetValueOrDefault(assigned.ChoreId, "a chore");
                    
                    var isClaimed = !assigned.AssignToAll 
                        && assigned.AssignedToMemberIds?.Length == 1 
                        && assigned.AssignedByMemberId.HasValue
                        && assigned.AssignedToMemberIds[0] == assigned.AssignedByMemberId.Value;

                    string assignText;
                    if (isClaimed)
                    {
                        assignText = $"{assignerNickname} claimed {choreName}";
                    }
                    else if (assigned.AssignToAll)
                    {
                        assignText = $"{choreName} assigned to everyone";
                    }
                    else
                    {
                        var assigneeNames = assigned.AssignedToMemberIds?
                            .Select(id => memberNicknames.GetValueOrDefault(id, "Unknown"))
                            .ToArray() ?? [];
                        assignText = $"{choreName} assigned to {string.Join(", ", assigneeNames)}";
                    }

                    activities.Add(new ActivityItem(
                        "chore_assigned",
                        assigned.TimestampUtc,
                        assignText,
                        assigned.ChoreId,
                        choreName,
                        assigned.AssignedByMemberId,
                        assignerNickname
                    ));
                    break;

                case ChoreCompleted completed:
                    var completerNickname = memberNicknames.GetValueOrDefault(completed.CompletedByMemberId, "Someone");
                    var completedChoreName = choreNames.GetValueOrDefault(completed.ChoreId, "a chore");
                    activities.Add(new ActivityItem(
                        "completion",
                        completed.CompletedAt.ToString("O"),
                        $"{completerNickname} completed {completedChoreName}",
                        completed.ChoreId,
                        completedChoreName,
                        completed.CompletedByMemberId,
                        completerNickname
                    ));
                    break;

                case ChoreDeleted deleted:
                    var deleterNickname = memberNicknames.GetValueOrDefault(deleted.DeletedByMemberId, "Someone");
                    var deletedChoreName = choreNames.GetValueOrDefault(deleted.ChoreId, "a chore");
                    activities.Add(new ActivityItem(
                        "chore_deleted",
                        deleted.TimestampUtc,
                        $"{deleterNickname} deleted {deletedChoreName}",
                        deleted.ChoreId,
                        deletedChoreName,
                        deleted.DeletedByMemberId,
                        deleterNickname
                    ));
                    break;
            }
        }

        // Sort by timestamp descending
        var sortedActivities = activities
            .OrderByDescending(a => DateTime.TryParse(a.TimestampUtc, out var ts) ? ts : DateTime.MinValue)
            .Take(500) // Keep last 500
            .ToList();

        var data = new ActivityReadModelData(
            new Dictionary<string, long>
            {
                ["household"] = householdEvents.Count,
                ["chores"] = choreEvents.Count
            },
            sortedActivities
        );

        var filePath = GetFilePath(householdId);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(data, JsonOptions));
    }
}
