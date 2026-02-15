using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Infrastructure;

/// <summary>
/// Decorator that publishes events via MediatR after they're successfully saved.
/// This enables SignalR handlers to broadcast events to connected clients.
/// </summary>
public class PublishingEventStore(IEventStore inner, IPublisher publisher) : IEventStore
{
    // StartStreamAsync overloads
    public Task<long> StartStreamAsync(StreamId streamId, IStoreableEvent @event)
    {
        return inner.StartStreamAsync(streamId, @event);
    }

    public Task<long> StartStreamAsync(StreamId streamId, string? correlationId, IEnumerable<IStoreableEvent> events)
    {
        return inner.StartStreamAsync(streamId, correlationId, events);
    }

    public Task<long> StartStreamAsync(StreamId streamId, IEnumerable<IStoreableEvent> events)
    {
        return inner.StartStreamAsync(streamId, events);
    }

    // AppendToStreamAsync overloads
    public async Task<long> AppendToStreamAsync(StreamId streamId, IStoreableEvent @event, ExpectedVersion expectedVersion)
    {
        var version = await inner.AppendToStreamAsync(streamId, @event, expectedVersion);
        await PublishEventAsync(@event);
        return version;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, string? correlationId, IStoreableEvent @event, ExpectedVersion expectedVersion)
    {
        var version = await inner.AppendToStreamAsync(streamId, correlationId, @event, expectedVersion);
        await PublishEventAsync(@event);
        return version;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        var version = await inner.AppendToStreamAsync(streamId, events, expectedVersion);
        foreach (var @event in events)
        {
            await PublishEventAsync(@event);
        }
        return version;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, string? correlationId, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        var version = await inner.AppendToStreamAsync(streamId, correlationId, events, expectedVersion);
        foreach (var @event in events)
        {
            await PublishEventAsync(@event);
        }
        return version;
    }
    
    private async Task PublishEventAsync(IStoreableEvent @event)
    {
        if (@event is INotification notification)
        {
            try
            {
                Console.WriteLine($"[SignalR] Publishing event: {notification.GetType().Name}");
                await publisher.Publish(notification);
                Console.WriteLine($"[SignalR] Published event: {notification.GetType().Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalR] Error publishing {notification.GetType().Name}: {ex.Message}");
            }
        }
    }

    // Fetch methods
    public Task<IReadOnlyList<StoredEvent>> FetchStreamAsync(StreamId streamId)
    {
        return inner.FetchStreamAsync(streamId);
    }

    public Task<IReadOnlyList<IStoreableEvent>> FetchEventsAsync(StreamId streamId)
    {
        return inner.FetchEventsAsync(streamId);
    }

    // Utility methods
    public Task<long> GetStreamVersionAsync(StreamId streamId)
    {
        return inner.GetStreamVersionAsync(streamId);
    }

    public Task<bool> StreamExistsAsync(StreamId streamId)
    {
        return inner.StreamExistsAsync(streamId);
    }
}
