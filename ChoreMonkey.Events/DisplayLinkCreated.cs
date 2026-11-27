namespace ChoreMonkey.Events;

/// <summary>An admin made a display link for the household. The link's secret never goes in an event: only its id and label do.</summary>
public record DisplayLinkCreated(Guid HouseholdId, Guid LinkId, string Label) : EventBase;
