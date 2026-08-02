using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Logistics.Tracking.Infrastructure.Persistence;

/// <summary>Design-time factory cho `dotnet ef`. DB riêng của Tracking (database-per-service).</summary>
public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TRACKING_DB")
            ?? "Host=localhost;Port=5433;Database=tracking;Username=logi;Password=logi";

        var options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new TrackingDbContext(options);
    }
}
