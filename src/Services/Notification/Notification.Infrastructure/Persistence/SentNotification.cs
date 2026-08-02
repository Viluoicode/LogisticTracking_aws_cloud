namespace Logistics.Notification.Infrastructure.Persistence;

/// <summary>Log một thông báo đã "gửi" cho người nhận (mô phỏng bằng ghi DB + log).</summary>
public sealed class SentNotification
{
    public Guid Id { get; set; }
    public string TrackingCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime OccurredOnUtc { get; set; }
    public DateTime SentAtUtc { get; set; }
}
