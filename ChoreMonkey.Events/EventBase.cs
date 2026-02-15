using FileEventStore;
using MediatR;

namespace ChoreMonkey.Events;

public record EventBase : IStoreableEvent, INotification
{
    public string TimestampUtc { get; set ; } = DateTime.UtcNow.ToString("o");
}
