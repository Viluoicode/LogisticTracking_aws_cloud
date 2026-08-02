using Logistics.Tracking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Tracking.Infrastructure.Persistence;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<TrackingEntry> TrackingEntries => Set<TrackingEntry>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackingEntry>(e =>
        {
            e.ToTable("tracking_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.TrackingCode).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.HasIndex(x => x.TrackingCode); // tra cứu timeline theo mã
        });

        modelBuilder.Entity<ProcessedMessage>(e =>
        {
            e.ToTable("processed_messages");
            e.HasKey(x => x.MessageId);
        });
    }
}
