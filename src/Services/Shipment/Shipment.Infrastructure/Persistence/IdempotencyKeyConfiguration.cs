using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Logistics.Shipment.Infrastructure.Persistence;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(200);
        builder.Property(x => x.TrackingCode).IsRequired();
    }
}
