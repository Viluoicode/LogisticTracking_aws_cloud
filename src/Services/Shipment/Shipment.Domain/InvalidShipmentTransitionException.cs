namespace Logistics.Shipment.Domain;

/// <summary>Ném khi cố chuyển trạng thái không hợp lệ theo state machine.</summary>
public sealed class InvalidShipmentTransitionException : Exception
{
    public InvalidShipmentTransitionException(ShipmentStatus from, ShipmentStatus to)
        : base($"Invalid shipment transition: {from} -> {to}.")
    {
    }
}
