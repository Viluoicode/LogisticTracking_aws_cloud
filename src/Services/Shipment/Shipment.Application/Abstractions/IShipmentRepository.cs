using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Application.Abstractions;

/// <summary>Port: cách Application thao tác Shipment mà không biết EF/DB.</summary>
public interface IShipmentRepository
{
    Task AddAsync(Domain.Shipment shipment, CancellationToken ct);

    Task<Domain.Shipment?> GetByCodeAsync(string trackingCode, CancellationToken ct);
}
