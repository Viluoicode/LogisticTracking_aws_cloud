using System.Text.Json;
using Logistics.BuildingBlocks.Domain;
using Logistics.Shared.Contracts;
using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Infrastructure.Persistence;

// DbContext đóng luôn vai IUnitOfWork: SaveChangesAsync(CancellationToken) đã sẵn có.
public sealed class ShipmentDbContext : DbContext, IUnitOfWork
{
    public ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : base(options) { }

    public DbSet<Domain.Shipment> Shipments => Set<Domain.Shipment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentDbContext).Assembly);
    }

    // Outbox pattern: gom domain event -> outbox row TRONG CÙNG transaction với thay đổi shipment.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutbox();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ConvertDomainEventsToOutbox()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                if (domainEvent is Domain.ShipmentStatusChangedDomainEvent e)
                {
                    var integrationEvent = new ShipmentStatusChangedIntegrationEvent(
                        e.ShipmentId, e.TrackingCode, e.Status.ToString(), e.OccurredOnUtc);

                    OutboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Type = nameof(ShipmentStatusChangedIntegrationEvent),
                        Content = JsonSerializer.Serialize(integrationEvent),
                        OccurredOnUtc = e.OccurredOnUtc,
                        // B9: bắt trace hiện tại (đang trong Activity của HTTP request) để truyền đi cùng event.
                        TraceParent = System.Diagnostics.Activity.Current?.Id
                    });
                }
            }

            aggregate.ClearDomainEvents();
        }
    }
}
