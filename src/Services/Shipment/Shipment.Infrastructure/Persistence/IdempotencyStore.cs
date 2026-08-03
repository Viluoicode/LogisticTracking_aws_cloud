using Logistics.Shipment.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Shipment.Infrastructure.Persistence;

public sealed class IdempotencyStore(ShipmentDbContext db) : IIdempotencyStore
{
    public async Task<string?> GetTrackingCodeAsync(string key, CancellationToken ct)
        => await db.Set<IdempotencyKey>()
            .Where(k => k.Key == key)
            .Select(k => k.TrackingCode)
            .FirstOrDefaultAsync(ct);

    public async Task SaveAsync(string key, string trackingCode, CancellationToken ct)
    {
        db.Set<IdempotencyKey>().Add(new IdempotencyKey
        {
            Key = key,
            TrackingCode = trackingCode,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
