using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Application.Contracts;
using MediatR;

namespace Logistics.Shipment.Application.Features.GetShipment;

public sealed record GetShipmentQuery(string TrackingCode) : IRequest<ShipmentResponse?>;

public sealed class GetShipmentHandler(IShipmentRepository repo)
    : IRequestHandler<GetShipmentQuery, ShipmentResponse?>
{
    public async Task<ShipmentResponse?> Handle(GetShipmentQuery request, CancellationToken ct)
    {
        var s = await repo.GetByCodeAsync(request.TrackingCode, ct);
        if (s is null) return null;

        return new ShipmentResponse(
            s.Code.Value,
            s.Status.ToString(),
            new AddressDto(s.Origin.Line, s.Origin.City, s.Origin.PostalCode),
            new AddressDto(s.Destination.Line, s.Destination.City, s.Destination.PostalCode),
            s.CreatedAtUtc,
            s.History.Select(h => new StatusChangeDto(h.Status.ToString(), h.OccurredAtUtc)).ToList());
    }
}
