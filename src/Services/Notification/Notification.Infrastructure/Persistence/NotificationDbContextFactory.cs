using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Logistics.Notification.Infrastructure.Persistence;

/// <summary>Design-time factory cho `dotnet ef`. DB riêng của Notification (database-per-service).</summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("NOTIFICATION_DB")
            ?? "Host=localhost;Port=5433;Database=notification;Username=logi;Password=logi";

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new NotificationDbContext(options);
    }
}
