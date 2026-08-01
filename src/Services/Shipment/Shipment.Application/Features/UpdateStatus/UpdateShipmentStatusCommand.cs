using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Application.Exceptions;
using MediatR;

namespace Logistics.Shipment.Application.Features.UpdateStatus;

public sealed record UpdateShipmentStatusCommand(string TrackingCode, string Action) : IRequest;

public sealed class UpdateShipmentStatusHandler(IShipmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<UpdateShipmentStatusCommand>
{
    public async Task Handle(UpdateShipmentStatusCommand request, CancellationToken ct)
    {
        var shipment = await repo.GetByCodeAsync(request.TrackingCode, ct)
            ?? throw new ShipmentNotFoundException(request.TrackingCode);

        // Ép luật chuyển trạng thái sống trong Domain — handler chỉ điều phối.
        switch (request.Action.Trim().ToLowerInvariant())
        {
            case "pickedup":       shipment.MarkPickedUp(); break;
            case "intransit":      shipment.MarkInTransit(); break;
            case "outfordelivery": shipment.MarkOutForDelivery(); break;
            case "delivered":      shipment.MarkDelivered(); break;
            case "failed":         shipment.MarkFailed(); break;
            case "returned":       shipment.MarkReturned(); break;
            default: throw new ArgumentException($"Unknown action '{request.Action}'.");
        }

        await uow.SaveChangesAsync(ct);
    }
}
