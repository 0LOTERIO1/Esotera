using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMelhorEnvioShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MelhorEnvioShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelectedDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuotedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ChargedFreightPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DeliveryTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MelhorEnvioShipmentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MelhorEnvioProtocol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TrackingCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TrackingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LabelUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CartCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LabelGeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSyncErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MelhorEnvioShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MelhorEnvioShipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MelhorEnvioShipments_MelhorEnvioShipmentId",
                table: "MelhorEnvioShipments",
                column: "MelhorEnvioShipmentId",
                unique: true,
                filter: "\"MelhorEnvioShipmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MelhorEnvioShipments_OrderId",
                table: "MelhorEnvioShipments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MelhorEnvioShipments_Status",
                table: "MelhorEnvioShipments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MelhorEnvioShipments");
        }
    }
}
