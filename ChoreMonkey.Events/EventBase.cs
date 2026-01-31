using FileEventStore;

namespace ChoreMonkey.Events;

public record EventBase : IStoreableEvent
{
    public string TimestampUtc { get; set ; } = DateTime.UtcNow.ToString("o");
}
