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
        
        // Publish event for SignalR handlers (fire and forget, don't block the request)
        if (@event is INotification notification)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await publisher.Publish(notification);
                }
                catch
                {
                    // Ignore publishing errors - SignalR broadcast is best-effort
                }
            });
        }
        
        return version;
    }

    public Task<long> AppendToStreamAsync(StreamId streamId, string? correlationId, IStoreableEvent @event, ExpectedVersion expectedVersion)
    {
        return inner.AppendToStreamAsync(streamId, correlationId, @event, expectedVersion);
    }

    public Task<long> AppendToStreamAsync(StreamId streamId, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        return inner.AppendToStreamAsync(streamId, events, expectedVersion);
    }

    public Task<long> AppendToStreamAsync(StreamId streamId, string? correlationId, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        return inner.AppendToStreamAsync(streamId, correlationId, events, expectedVersion);
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
