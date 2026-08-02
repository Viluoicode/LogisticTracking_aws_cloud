using Logistics.Notification.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Notification.Infrastructure.Persistence;

public sealed class NotificationStore(NotificationDbContext db) : INotificationStore
{
    public async Task<bool> TryRecordAsync(string trackingCode, string status, DateTime occurredOnUtc, string messageId, CancellationToken ct)
    {
        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId, ct))
            return false; // trùng -> bỏ qua

        db.SentNotifications.Add(new SentNotification
        {
            Id = Guid.NewGuid(),
            TrackingCode = trackingCode,
            Status = status,
            OccurredOnUtc = occurredOnUtc,
            SentAtUtc = DateTime.UtcNow
        });
        db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId, ProcessedOnUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
