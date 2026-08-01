using Logistics.BuildingBlocks.Domain;

namespace Logistics.Shipment.Domain;

/// <summary>Phát mỗi khi shipment đổi trạng thái. M5 sẽ dịch thành integration event lên SNS.</summary>
public sealed record ShipmentStatusChangedDomainEvent(
    Guid ShipmentId,
    string TrackingCode,
    ShipmentStatus Status) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
