using Logistics.Shipment.Application.Abstractions;
using Logistics.Shipment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.Shipment.Infrastructure.Outbox;

/// <summary>
/// Chạy nền: quét outbox row chưa xử lý -> publish lên SNS -> đánh dấu ProcessedOnUtc.
/// Publish fail thì để nguyên (ProcessedOnUtc null) -> tick sau tự retry. Giải bài toán dual-write.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Outbox dispatch loop error"); }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Content, ct);
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;   // giữ lại để retry lần sau
                logger.LogError(ex, "Failed to publish outbox message {Id}", message.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
