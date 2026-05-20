using System;
using MarketAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAgent.Infrastructure.Migrations;

[DbContext(typeof(MarketAgentDbContext))]
[Migration("20260520130000_AddSignalSnapshots")]
public partial class AddSignalSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SignalSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Setup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                Confidence = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                RelativeStrengthVsSpy = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                RelativeVolume = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                ExtensionFromEma20Percent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                MarketRegime = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                TriggeredAlertsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Ema9 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                Ema20 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                Ema50 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                Rsi = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Timeframe = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SignalType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Entry = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                Stop = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                Target = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                ScoreBreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OpeningRedReversalDetected = table.Column<bool>(type: "bit", nullable: false),
                OpenGapPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                RecoveryFromLowPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SignalSnapshots", snapshot => snapshot.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SignalSnapshots_CreatedAtUtc",
            table: "SignalSnapshots",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_SignalSnapshots_RunId",
            table: "SignalSnapshots",
            column: "RunId");

        migrationBuilder.CreateIndex(
            name: "IX_SignalSnapshots_Symbol",
            table: "SignalSnapshots",
            column: "Symbol");

        migrationBuilder.CreateIndex(
            name: "IX_SignalSnapshots_Symbol_CreatedAtUtc",
            table: "SignalSnapshots",
            columns: ["Symbol", "CreatedAtUtc"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SignalSnapshots");
    }
}
