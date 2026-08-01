using Microsoft.EntityFrameworkCore;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Infrastructure.Persistence;

public sealed class ShipmentDbContext : DbContext
{
    public ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : base(options) { }

    public DbSet<Domain.Shipment> Shipments => Set<Domain.Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentDbContext).Assembly);
    }
}
