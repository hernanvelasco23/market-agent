using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAgent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignalSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Setup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PriceAtSignal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_SignalSnapshots_SignalSnapshotId",
                        column: x => x.SignalSnapshotId,
                        principalTable: "SignalSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_CreatedAtUtc",
                table: "AlertEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_SignalSnapshotId_AlertType",
                table: "AlertEvents",
                columns: new[] { "SignalSnapshotId", "AlertType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_Symbol",
                table: "AlertEvents",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");
        }
    }
}
