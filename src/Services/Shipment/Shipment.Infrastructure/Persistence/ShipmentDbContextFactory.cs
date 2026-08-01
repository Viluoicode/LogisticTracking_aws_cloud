using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Logistics.Shipment.Infrastructure.Persistence;

/// <summary>
/// Design-time factory: cho phép `dotnet ef migrations`/`database update` chạy
/// mà không cần host (Api). Connection lấy từ env SHIPMENT_DB, mặc định Postgres local.
/// </summary>
public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        // Port 5433: Postgres của docker-compose (5432 bị Postgres native trên Windows chiếm).
        var connection = Environment.GetEnvironmentVariable("SHIPMENT_DB")
            ?? "Host=localhost;Port=5433;Database=logistics;Username=logi;Password=logi";

        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new ShipmentDbContext(options);
    }
}
