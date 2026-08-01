using Logistics.Shipment.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Infrastructure.Persistence;

// DbContext đóng luôn vai IUnitOfWork: SaveChangesAsync(CancellationToken) đã sẵn có.
public sealed class ShipmentDbContext : DbContext, IUnitOfWork
{
    public ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : base(options) { }

    public DbSet<Domain.Shipment> Shipments => Set<Domain.Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentDbContext).Assembly);
    }
}
