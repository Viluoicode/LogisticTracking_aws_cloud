namespace Logistics.Tracking.Application.Contracts;

public sealed record TrackingPointDto(string Status, DateTime OccurredOnUtc);

public sealed record TrackingTimelineResponse(
    string TrackingCode,
    string CurrentStatus,
    IReadOnlyList<TrackingPointDto> Timeline);
