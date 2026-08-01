namespace Logistics.Shipment.Application.Exceptions;

public sealed class ShipmentNotFoundException(string trackingCode)
    : Exception($"Shipment with tracking code '{trackingCode}' was not found.");
