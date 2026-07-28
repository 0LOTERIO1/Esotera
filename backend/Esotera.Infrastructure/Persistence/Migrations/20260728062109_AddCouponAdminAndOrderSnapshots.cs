using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponAdminAndOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscountApplied",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CouponId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponMinPurchaseSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponNominalDiscount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeShippingMinSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeShippingStatesSnapshot",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "J3CutoffHourSnapshot",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "J3PriceSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingSubsidyAmountSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingSubsidyEnabledSnapshot",
                table: "Orders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Coupons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxTotalUses",
                table: "Coupons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_IsActive",
                table: "Coupons",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_IsArchived",
                table: "Coupons",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Coupons_IsActive",
                table: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_Coupons_IsArchived",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "CouponDiscountApplied",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponMinPurchaseSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponNominalDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FreeShippingMinSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FreeShippingStatesSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "J3CutoffHourSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "J3PriceSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingSubsidyAmountSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingSubsidyEnabledSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "MaxTotalUses",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Coupons");
        }
    }
}
