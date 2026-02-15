using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Infrastructure;

/// <summary>
/// Decorator that publishes events via MediatR after they're appended to the store.
/// This enables SignalR and other subscribers to react to domain events.
/// </summary>
public class PublishingEventStore(IEventStore inner, IMediator mediator) : IEventStore
{
    /// <summary>
    /// The underlying event store (useful for automations to avoid circular publishing)
    /// </summary>
    public IEventStore Inner => inner;

    public async Task<long> StartStreamAsync(StreamId streamId, string? streamType, IEnumerable<IStoreableEvent> events)
    {
        var eventList = events.ToList();
        var result = await inner.StartStreamAsync(streamId, streamType, eventList);
        await PublishEventsAsync(eventList);
        return result;
    }

    public async Task<long> StartStreamAsync(StreamId streamId, IEnumerable<IStoreableEvent> events)
    {
        var eventList = events.ToList();
        var result = await inner.StartStreamAsync(streamId, eventList);
        await PublishEventsAsync(eventList);
        return result;
    }

    public async Task<long> StartStreamAsync(StreamId streamId, IStoreableEvent evt)
    {
        var result = await inner.StartStreamAsync(streamId, evt);
        await PublishEventsAsync([evt]);
        return result;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        var eventList = events.ToList();
        var result = await inner.AppendToStreamAsync(streamId, eventList, expectedVersion);
        await PublishEventsAsync(eventList);
        return result;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, IStoreableEvent evt, ExpectedVersion expectedVersion)
    {
        var result = await inner.AppendToStreamAsync(streamId, evt, expectedVersion);
        await PublishEventsAsync([evt]);
        return result;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, string? streamType, IEnumerable<IStoreableEvent> events, ExpectedVersion expectedVersion)
    {
        var eventList = events.ToList();
        var result = await inner.AppendToStreamAsync(streamId, streamType, eventList, expectedVersion);
        await PublishEventsAsync(eventList);
        return result;
    }

    public async Task<long> AppendToStreamAsync(StreamId streamId, string? streamType, IStoreableEvent evt, ExpectedVersion expectedVersion)
    {
        var result = await inner.AppendToStreamAsync(streamId, streamType, evt, expectedVersion);
        await PublishEventsAsync([evt]);
        return result;
    }

    public Task<IReadOnlyList<StoredEvent>> FetchStreamAsync(StreamId streamId)
        => inner.FetchStreamAsync(streamId);

    public Task<IReadOnlyList<IStoreableEvent>> FetchEventsAsync(StreamId streamId)
        => inner.FetchEventsAsync(streamId);

    public Task<long> GetStreamVersionAsync(StreamId streamId)
        => inner.GetStreamVersionAsync(streamId);

    public Task<bool> StreamExistsAsync(StreamId streamId)
        => inner.StreamExistsAsync(streamId);

    private async Task PublishEventsAsync(IEnumerable<IStoreableEvent> events)
    {
        foreach (var evt in events)
        {
            if (evt is INotification notification)
            {
                await mediator.Publish(notification);
            }
        }
    }
}
