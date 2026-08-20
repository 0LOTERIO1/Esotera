using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemSkuSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "OrderItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sku",
                table: "OrderItems");
        }
    }
}
