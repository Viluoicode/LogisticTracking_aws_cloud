using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Shipment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShipmentInfrastructure(this IServiceCollection services)
    {
        var connectionString = BuildConnectionString();

        services.AddDbContext<ShipmentDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        // DbContext đóng luôn vai IUnitOfWork (cùng 1 instance/scope).
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShipmentDbContext>());

        return services;
    }

    /// <summary>
    /// AWS/ECS: đọc DB_* (tiêm từ Secrets Manager). Local: fallback Postgres docker-compose (host 5433).
    /// </summary>
    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        if (!string.IsNullOrWhiteSpace(host))
        {
            var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
            var user = Environment.GetEnvironmentVariable("DB_USER") ?? "logi";
            var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
            var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "logistics";
            return $"Host={host};Port={port};Database={name};Username={user};Password={pass}";
        }

        return Environment.GetEnvironmentVariable("SHIPMENT_DB")
            ?? "Host=localhost;Port=5433;Database=logistics;Username=logi;Password=logi";
    }
}
