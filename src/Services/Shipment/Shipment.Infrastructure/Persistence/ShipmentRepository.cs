using Logistics.Shipment.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Infrastructure.Persistence;

/// <summary>Adapter: hiện thực port bằng EF Core. History (owned) tự load kèm.</summary>
public sealed class ShipmentRepository(ShipmentDbContext db) : IShipmentRepository
{
    public async Task AddAsync(Domain.Shipment shipment, CancellationToken ct)
        => await db.Shipments.AddAsync(shipment, ct);

    public async Task<Domain.Shipment?> GetByCodeAsync(string trackingCode, CancellationToken ct)
    {
        var code = Domain.TrackingCode.From(trackingCode);
        return await db.Shipments.FirstOrDefaultAsync(s => s.Code == code, ct);
    }
}
