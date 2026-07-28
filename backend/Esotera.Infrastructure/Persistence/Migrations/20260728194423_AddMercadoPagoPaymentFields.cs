using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPaymentId",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPaymentStatus",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentIdempotencyKey",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MercadoPagoPaymentId",
                table: "Orders",
                column: "MercadoPagoPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_MercadoPagoPaymentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPaymentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentIdempotencyKey",
                table: "Orders");
        }
    }
}
