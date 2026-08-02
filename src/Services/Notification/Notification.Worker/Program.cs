using Logistics.Notification.Infrastructure;
using Logistics.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// M7: log có cấu trúc (Serilog).
builder.Services.AddSerilog(config => config.WriteTo.Console());
builder.Services.AddNotificationInfrastructure();

var host = builder.Build();

// Auto-migrate (dev/demo). Tạo DB "notification" nếu chưa có (database-per-service).
using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.Migrate();
}

host.Run();
