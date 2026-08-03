using Amazon.Runtime;
using Amazon.SQS;
using Logistics.Tracking.Application.Abstractions;
using Logistics.Tracking.Infrastructure.Messaging;
using Logistics.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logistics.Tracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<TrackingDbContext>(options =>
            options.UseNpgsql(BuildConnectionString(), npg => npg.CommandTimeout(30))); // B8: DB command timeout
        services.AddScoped<ITrackingReadStore, TrackingReadStore>();

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var config = new AmazonSQSConfig { ServiceURL = endpoint, AuthenticationRegion = "ap-southeast-1" };
                return new AmazonSQSClient(new BasicAWSCredentials("test", "test"), config);
            }
            return new AmazonSQSClient();
        });
        services.AddHostedService<TrackingQueueConsumer>();

        return services;
    }

    private static string BuildConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        if (!string.IsNullOrWhiteSpace(host))
        {
            var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
            var user = Environment.GetEnvironmentVariable("DB_USER") ?? "logi";
            var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
            var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "tracking";
            return $"Host={host};Port={port};Database={name};Username={user};Password={pass}";
        }

        return Environment.GetEnvironmentVariable("TRACKING_DB")
            ?? "Host=localhost;Port=5433;Database=tracking;Username=logi;Password=logi";
    }
}
