namespace Logistics.Shipment.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<string?> GetTrackingCodeAsync(string key, CancellationToken ct);
    Task SaveAsync(string key, string trackingCode, CancellationToken ct);
}
