namespace Logistics.Notification.Infrastructure.Persistence;

/// <summary>Message-id đã xử lý — idempotency (SQS at-least-once).</summary>
public sealed class ProcessedMessage
{
    public string MessageId { get; set; } = null!;
    public DateTime ProcessedOnUtc { get; set; }
}
