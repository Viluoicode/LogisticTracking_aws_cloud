namespace Logistics.Shipment.Application.Contracts;

public sealed record AddressDto(string Line, string City, string PostalCode);

public sealed record StatusChangeDto(string Status, DateTime OccurredAtUtc);

public sealed record ShipmentResponse(
    string Code,
    string Status,
    AddressDto Origin,
    AddressDto Destination,
    DateTime CreatedAtUtc,
    IReadOnlyList<StatusChangeDto> History);
