using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAgent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignalSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PriceAtSignal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceAfter15Minutes = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceAfter1Hour = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceAfter4Hours = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PriceAfter1Day = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    MaxRunupPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    MaxDrawdownPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    OutcomePercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalOutcomes_SignalSnapshots_SignalSnapshotId",
                        column: x => x.SignalSnapshotId,
                        principalTable: "SignalSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_EvaluatedAtUtc",
                table: "SignalOutcomes",
                column: "EvaluatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_EvaluationStatus",
                table: "SignalOutcomes",
                column: "EvaluationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_IsSuccessful",
                table: "SignalOutcomes",
                column: "IsSuccessful");

            migrationBuilder.CreateIndex(
                name: "IX_SignalOutcomes_SignalSnapshotId",
                table: "SignalOutcomes",
                column: "SignalSnapshotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalOutcomes");
        }
    }
}
