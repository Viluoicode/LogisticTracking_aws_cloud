using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Infrastructure.Messaging;
using Logistics.Shipment.Infrastructure.Outbox;
using Logistics.Shipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logistics.Shipment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShipmentInfrastructure(this IServiceCollection services)
    {
        var connectionString = BuildConnectionString();

        services.AddDbContext<ShipmentDbContext>(options =>
            options.UseNpgsql(connectionString, npg => npg.CommandTimeout(30))); // B8: DB command timeout
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        // DbContext đóng luôn vai IUnitOfWork (cùng 1 instance/scope).
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShipmentDbContext>());

        // Messaging: SNS client (LocalStack nếu có AWS_ENDPOINT_URL, ngược lại AWS thật) + publisher + dispatcher.
        services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
        {
            var endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var config = new AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = endpoint,
                    AuthenticationRegion = "ap-southeast-1"
                };
                return new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials("test", "test"), config);
            }
            return new AmazonSimpleNotificationServiceClient();
        });
        services.AddSingleton<IEventPublisher, SnsEventPublisher>();
        services.AddHostedService<OutboxDispatcher>();

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
