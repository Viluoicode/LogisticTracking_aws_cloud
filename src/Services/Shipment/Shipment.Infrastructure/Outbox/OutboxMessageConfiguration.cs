using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Logistics.Shipment.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.OccurredOnUtc).IsRequired();
        builder.Property(x => x.ProcessedOnUtc);
        builder.Property(x => x.Error);
        builder.Property(x => x.TraceParent);

        // Index cho dispatcher quét nhanh các row chưa xử lý.
        builder.HasIndex(x => x.ProcessedOnUtc);
    }
}
