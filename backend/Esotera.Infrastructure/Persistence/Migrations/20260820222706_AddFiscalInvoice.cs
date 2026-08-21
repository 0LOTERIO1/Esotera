using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Esotera.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChNFe = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    Number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Series = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Environment = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    RecipientDocument = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    ProtocolNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    XmlCipher = table.Column<string>(type: "text", nullable: false),
                    XmlSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalInvoices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalInvoices_ChNFe",
                table: "FiscalInvoices",
                column: "ChNFe",
                unique: true,
                filter: "\"ChNFe\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalInvoices_OrderId",
                table: "FiscalInvoices",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" = 'authorized'");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalInvoices_XmlSha256",
                table: "FiscalInvoices",
                column: "XmlSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalInvoices");
        }
    }
}
