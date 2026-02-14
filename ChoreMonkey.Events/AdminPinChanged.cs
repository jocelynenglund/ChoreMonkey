namespace ChoreMonkey.Events;

public record AdminPinChanged(Guid HouseholdId, string NewPinHash) : EventBase;
