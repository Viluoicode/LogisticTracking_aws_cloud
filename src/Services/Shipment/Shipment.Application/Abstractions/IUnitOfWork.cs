namespace Logistics.Shipment.Application.Abstractions;

/// <summary>Port: chốt (commit) mọi thay đổi trong một transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
