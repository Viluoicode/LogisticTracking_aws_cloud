using Logistics.Tracking.Application.Abstractions;
using Logistics.Tracking.Domain;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Tracking.Infrastructure.Persistence;

public sealed class TrackingReadStore(TrackingDbContext db) : ITrackingReadStore
{
    public async Task ApplyAsync(TrackingEntry entry, string messageId, CancellationToken ct)
    {
        // Idempotency: message trùng (SQS at-least-once) thì bỏ qua.
        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId, ct))
            return;

        db.TrackingEntries.Add(entry);
        db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId, ProcessedOnUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TrackingEntry>> GetTimelineAsync(string trackingCode, CancellationToken ct)
        => await db.TrackingEntries
            .Where(e => e.TrackingCode == trackingCode)
            .OrderBy(e => e.OccurredOnUtc)
            .ToListAsync(ct);
}
