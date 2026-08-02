using Logistics.Tracking.Domain;

namespace Logistics.Tracking.Application.Abstractions;

public interface ITrackingReadStore
{
    /// <summary>Idempotent: nếu messageId đã xử lý thì bỏ qua; ngược lại thêm entry + đánh dấu đã xử lý.</summary>
    Task ApplyAsync(TrackingEntry entry, string messageId, CancellationToken ct);

    Task<IReadOnlyList<TrackingEntry>> GetTimelineAsync(string trackingCode, CancellationToken ct);
}
