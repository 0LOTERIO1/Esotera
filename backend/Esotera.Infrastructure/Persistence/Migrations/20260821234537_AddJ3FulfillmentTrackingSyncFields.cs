using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJ3FulfillmentTrackingSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "J3LastStatusSyncAtUtc",
                table: "J3Fulfillments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "J3LastStatusSyncErrorAtUtc",
                table: "J3Fulfillments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "J3LastStatusSyncErrorCode",
                table: "J3Fulfillments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "J3RemoteStatus",
                table: "J3Fulfillments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "J3LastStatusSyncAtUtc",
                table: "J3Fulfillments");

            migrationBuilder.DropColumn(
                name: "J3LastStatusSyncErrorAtUtc",
                table: "J3Fulfillments");

            migrationBuilder.DropColumn(
                name: "J3LastStatusSyncErrorCode",
                table: "J3Fulfillments");

            migrationBuilder.DropColumn(
                name: "J3RemoteStatus",
                table: "J3Fulfillments");
        }
    }
}
