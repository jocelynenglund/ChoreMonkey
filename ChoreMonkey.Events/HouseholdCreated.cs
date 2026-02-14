using FileEventStore;

namespace ChoreMonkey.Events;

public record HouseholdCreated(
    Guid HouseholdId, 
    string Name, 
    string PinHash,
    string? MemberPinHash = null) : EventBase;