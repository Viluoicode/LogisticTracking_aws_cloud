namespace Logistics.Shipment.Infrastructure.Persistence;

/// <summary>B11: lưu Idempotency-Key -> kết quả, để POST lặp (client retry) không tạo trùng.</summary>
public sealed class IdempotencyKey
{
    public string Key { get; set; } = null!;
    public string TrackingCode { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
