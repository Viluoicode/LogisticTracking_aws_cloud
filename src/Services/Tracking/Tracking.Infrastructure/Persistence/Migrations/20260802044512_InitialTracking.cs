using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_messages",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "tracking_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tracking_entries_TrackingCode",
                table: "tracking_entries",
                column: "TrackingCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_messages");

            migrationBuilder.DropTable(
                name: "tracking_entries");
        }
    }
}
