using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shipment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceParentAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                table: "outbox_messages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TrackingCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                table: "outbox_messages");
        }
    }
}
