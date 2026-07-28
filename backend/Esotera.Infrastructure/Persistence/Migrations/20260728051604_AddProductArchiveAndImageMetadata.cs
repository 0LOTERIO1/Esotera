using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductArchiveAndImageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserva URLs existentes: renomeia UrlOrPath → SecureUrl e amplia o tamanho
            migrationBuilder.RenameColumn(
                name: "UrlOrPath",
                table: "ProductImages",
                newName: "SecureUrl");

            migrationBuilder.AlterColumn<string>(
                name: "SecureUrl",
                table: "ProductImages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "Products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "ProductImages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProductImages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "ProductImages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsArchived",
                table: "Products",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_PublicId",
                table: "ProductImages",
                column: "PublicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsArchived",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_PublicId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AltText",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "ProductImages");

            migrationBuilder.AlterColumn<string>(
                name: "SecureUrl",
                table: "ProductImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.RenameColumn(
                name: "SecureUrl",
                table: "ProductImages",
                newName: "UrlOrPath");
        }
    }
}
