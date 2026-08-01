using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Application.Contracts;
using MediatR;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Application.Features.CreateShipment;

public sealed record CreateShipmentCommand(AddressDto Origin, AddressDto Destination) : IRequest<string>;

public sealed class CreateShipmentHandler(IShipmentRepository repo, IUnitOfWork uow)
    : IRequestHandler<CreateShipmentCommand, string>
{
    public async Task<string> Handle(CreateShipmentCommand request, CancellationToken ct)
    {
        var origin = new Domain.Address(request.Origin.Line, request.Origin.City, request.Origin.PostalCode);
        var destination = new Domain.Address(request.Destination.Line, request.Destination.City, request.Destination.PostalCode);

        var shipment = Domain.Shipment.Create(origin, destination);

        await repo.AddAsync(shipment, ct);
        await uow.SaveChangesAsync(ct);

        // shipment.DomainEvents đang chờ — M5 sẽ đẩy vào Outbox -> SNS.
        return shipment.Code.Value;
    }
}
