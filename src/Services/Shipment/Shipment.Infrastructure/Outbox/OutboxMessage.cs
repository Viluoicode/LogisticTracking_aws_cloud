namespace Logistics.Shipment.Infrastructure.Outbox;

/// <summary>
/// Một event chờ publish. Ghi trong CÙNG transaction với thay đổi shipment (Outbox pattern)
/// → atomic, không mất event khi SNS lỗi. Dispatcher (M5b) đọc row ProcessedOnUtc == null.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;       // tên integration event
    public string Content { get; set; } = null!;     // payload JSON
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }     // null = chưa publish
    public string? Error { get; set; }                // lỗi publish gần nhất (nếu có)
}
