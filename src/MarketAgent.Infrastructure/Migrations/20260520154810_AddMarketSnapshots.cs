using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAgent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AssetType = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OpenPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    HighPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    LowPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PreviousClose = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshots_CapturedAtUtc",
                table: "MarketSnapshots",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshots_Symbol",
                table: "MarketSnapshots",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshots_Symbol_CapturedAtUtc",
                table: "MarketSnapshots",
                columns: new[] { "Symbol", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshots_Symbol_Source_CapturedAtUtc",
                table: "MarketSnapshots",
                columns: new[] { "Symbol", "Source", "CapturedAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketSnapshots");
        }
    }
}
