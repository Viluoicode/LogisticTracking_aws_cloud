namespace Logistics.Shared.Contracts;

/// <summary>
/// Hợp đồng công khai giữa các service (schema "trên đường dây").
/// Chỉ record này được chia sẻ — KHÔNG share entity/DB nội bộ của Shipment.
/// Tracking/Notification deserialize đúng shape này từ SQS.
/// </summary>
public sealed record ShipmentStatusChangedIntegrationEvent(
    Guid ShipmentId,
    string TrackingCode,
    string Status,
    DateTime OccurredOnUtc);
