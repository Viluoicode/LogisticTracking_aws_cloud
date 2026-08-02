using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain = Logistics.Shipment.Domain;

namespace Logistics.Shipment.Infrastructure.Persistence;

/// <summary>
/// EF mapping cho aggregate Shipment — nơi DUY NHẤT biết về DB.
/// Value object -> cột (converter/owned), history -> bảng con, domain events -> bỏ qua.
/// </summary>
internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Domain.Shipment>
{
    public void Configure(EntityTypeBuilder<Domain.Shipment> builder)
    {
        builder.ToTable("shipments");
        builder.HasKey(s => s.Id);

        // A3: optimistic concurrency dùng cột hệ thống Postgres "xmin" (không thêm cột thật).
        // (Npgsql 9 gỡ UseXminAsConcurrencyToken → map shadow property thủ công.)
        // Hai update đồng thời -> update thứ 2 ném DbUpdateConcurrencyException -> API trả 409.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // TrackingCode (value object) -> cột string, unique
        builder.Property(s => s.Code)
            .HasConversion(code => code.Value, value => Domain.TrackingCode.From(value))
            .HasColumnName("tracking_code")
            .IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();

        // Enum -> lưu dạng string cho dễ đọc
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        // Address (value object) -> trải thành cột trong bảng shipments
        builder.OwnsOne(s => s.Origin, o =>
        {
            o.Property(p => p.Line).HasColumnName("origin_line").IsRequired();
            o.Property(p => p.City).HasColumnName("origin_city").IsRequired();
            o.Property(p => p.PostalCode).HasColumnName("origin_postal_code");
        });

        builder.OwnsOne(s => s.Destination, d =>
        {
            d.Property(p => p.Line).HasColumnName("destination_line").IsRequired();
            d.Property(p => p.City).HasColumnName("destination_city").IsRequired();
            d.Property(p => p.PostalCode).HasColumnName("destination_postal_code");
        });

        // Lịch sử trạng thái -> bảng con
        builder.OwnsMany(s => s.History, h =>
        {
            h.ToTable("shipment_status_history");
            h.WithOwner().HasForeignKey("shipment_id");
            h.Property(p => p.Status).HasConversion<string>().HasColumnName("status");
            h.Property(p => p.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });

        // Domain events chỉ sống trong RAM, không lưu DB
        builder.Ignore(s => s.DomainEvents);
    }
}
