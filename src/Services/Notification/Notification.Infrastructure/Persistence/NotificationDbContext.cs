using Microsoft.EntityFrameworkCore;

namespace Logistics.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<SentNotification> SentNotifications => Set<SentNotification>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentNotification>(e =>
        {
            e.ToTable("sent_notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.TrackingCode).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.HasIndex(x => x.TrackingCode);
        });

        modelBuilder.Entity<ProcessedMessage>(e =>
        {
            e.ToTable("processed_messages");
            e.HasKey(x => x.MessageId);
        });
    }
}
