using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMelhorEnvioQuoteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MelhorEnvioQuoteEnabled",
                table: "StoreSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageHeightCm",
                table: "StoreSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 6m);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageLengthCm",
                table: "StoreSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 16m);

            migrationBuilder.AddColumn<int>(
                name: "PackageWeightGrams",
                table: "StoreSettings",
                type: "integer",
                nullable: false,
                defaultValue: 400);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageWidthCm",
                table: "StoreSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 11m);

            migrationBuilder.AddColumn<string>(
                name: "ShippingOriginCep",
                table: "StoreSettings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "08061420");

            migrationBuilder.AddColumn<string>(
                name: "ShippingCarrierName",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingCompanyId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingDeliveryMaxDays",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingDeliveryMinDays",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingFreeShippingApplied",
                table: "Orders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingOriginalPrice",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingQuoteEnvironment",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippingQuotedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingServiceId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingServiceName",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingSubsidyApplied",
                table: "Orders",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MelhorEnvioQuoteEnabled",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PackageHeightCm",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PackageLengthCm",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PackageWeightGrams",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PackageWidthCm",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ShippingOriginCep",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ShippingCarrierName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingCompanyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingDeliveryMaxDays",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingDeliveryMinDays",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingFreeShippingApplied",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingOriginalPrice",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingQuoteEnvironment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingQuotedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingServiceId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingServiceName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingSubsidyApplied",
                table: "Orders");
        }
    }
}
