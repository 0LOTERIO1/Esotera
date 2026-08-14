using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJ3FulfillmentAndResidentialAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShippingIsResidentialAddress",
                table: "Orders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResidentialAddress",
                table: "Addresses",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "J3Fulfillments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    J3OrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    J3OrderCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    J3TrackingNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    J3DeliveryPointId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    J3StampUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_J3Fulfillments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_J3Fulfillments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_J3Fulfillments_J3OrderId",
                table: "J3Fulfillments",
                column: "J3OrderId",
                unique: true,
                filter: "\"J3OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_J3Fulfillments_OrderId",
                table: "J3Fulfillments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_J3Fulfillments_Status",
                table: "J3Fulfillments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "J3Fulfillments");

            migrationBuilder.DropColumn(
                name: "ShippingIsResidentialAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsResidentialAddress",
                table: "Addresses");
        }
    }
}
