namespace Logistics.Notification.Application.Abstractions;

public interface INotificationStore
{
    /// <summary>Idempotent: true nếu là message MỚI (đã ghi + đánh dấu); false nếu trùng (bỏ qua).</summary>
    Task<bool> TryRecordAsync(string trackingCode, string status, DateTime occurredOnUtc, string messageId, CancellationToken ct);
}
