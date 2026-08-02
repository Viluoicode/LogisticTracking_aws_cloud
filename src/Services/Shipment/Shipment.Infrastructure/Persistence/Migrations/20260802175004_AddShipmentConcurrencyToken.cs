using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shipment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "xmin" là CỘT HỆ THỐNG có sẵn trên mọi bảng Postgres — KHÔNG tạo cột.
            // Migration này chỉ để cập nhật model snapshot (concurrency token). Up/Down no-op.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
