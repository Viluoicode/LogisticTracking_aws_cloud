namespace Logistics.Tracking.Infrastructure.Persistence;

/// <summary>Bản ghi message-id đã xử lý — nền cho idempotency (SQS giao at-least-once).</summary>
public sealed class ProcessedMessage
{
    public string MessageId { get; set; } = null!;
    public DateTime ProcessedOnUtc { get; set; }
}
