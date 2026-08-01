namespace Logistics.Shipment.Domain;

/// <summary>Một mốc trong lịch sử trạng thái của shipment.</summary>
public sealed class StatusChange
{
    public ShipmentStatus Status { get; }
    public DateTime OccurredAtUtc { get; }

    private StatusChange() { } // EF materialization

    public StatusChange(ShipmentStatus status, DateTime occurredAtUtc)
    {
        Status = status;
        OccurredAtUtc = occurredAtUtc;
    }
}
