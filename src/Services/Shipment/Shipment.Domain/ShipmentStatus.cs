namespace Logistics.Shipment.Domain;

public enum ShipmentStatus
{
    Created,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    Failed,
    Returned
}
