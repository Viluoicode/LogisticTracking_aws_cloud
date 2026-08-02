using Logistics.Shipment.Application.Abstractions;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Application.Tests;

/// <summary>Fake in-memory (không cần thư viện mock) để test use case nhanh, không đụng DB.</summary>
internal sealed class FakeShipmentRepository : IShipmentRepository
{
    public List<Domain.Shipment> Items { get; } = new();

    public Task AddAsync(Domain.Shipment shipment, CancellationToken ct)
    {
        Items.Add(shipment);
        return Task.CompletedTask;
    }

    public Task<Domain.Shipment?> GetByCodeAsync(string trackingCode, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(s => s.Code.Value == trackingCode));
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
