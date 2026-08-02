using Amazon.Runtime;
using Amazon.SQS;
using Logistics.Notification.Application.Abstractions;
using Logistics.Notification.Infrastructure.Messaging;
using Logistics.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Logistics.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(BuildConnectionString()));
        services.AddScoped<INotificationStore, NotificationStore>();

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
        services.AddHostedService<NotifQueueConsumer>();

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
            var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "notification";
            return $"Host={host};Port={port};Database={name};Username={user};Password={pass}";
        }

        return Environment.GetEnvironmentVariable("NOTIFICATION_DB")
            ?? "Host=localhost;Port=5433;Database=notification;Username=logi;Password=logi";
    }
}
