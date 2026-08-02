using Logistics.Shipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Logistics.Shipment.Integration.Tests;

/// <summary>Bật một Postgres THẬT trong container cho test (không cần DB cài sẵn); chạy được cả CI.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync(); // áp toàn bộ migration -> tạo schema thật
    }

    public ShipmentDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
