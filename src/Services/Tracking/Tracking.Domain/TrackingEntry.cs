namespace Logistics.Tracking.Domain;

/// <summary>
/// Read-model: mỗi mốc trạng thái của 1 shipment, dựng từ integration event (CQRS read side).
/// Không phải aggregate giàu logic — đây là bản chiếu (projection) để tra cứu nhanh.
/// </summary>
public sealed class TrackingEntry
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string TrackingCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime OccurredOnUtc { get; set; }
}
