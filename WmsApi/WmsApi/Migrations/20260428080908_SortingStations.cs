using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SortingStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StationId",
                schema: "sorting",
                table: "SortingPallets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SortingBatchQueues",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PackingIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    AssignedPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingBatchQueues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SortingBatchQueues_SortingPallets_AssignedPalletId",
                        column: x => x.AssignedPalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "SortingPalletPacks",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingPalletPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SortingPalletPacks_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                    table.ForeignKey(
                        name: "FK_SortingPalletPacks_SortingPallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "SortingStations",
                schema: "sorting",
                columns: table => new
                {
                    StationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    DisabledBy = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisableReason = table.Column<string>(type: "nvarchar(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingStations", x => x.StationId);
                    table.ForeignKey(
                        name: "FK_SortingStations_SortingPallets_CurrentPalletId",
                        column: x => x.CurrentPalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "StationAuditLogs",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", nullable: true),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationAuditLogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "sorting",
                table: "SortingStations",
                columns: new[] { "StationId", "CurrentPalletId", "DisableReason", "DisabledAt", "DisabledBy", "Enabled" },
                values: new object[,]
                {
                    { 1, null, null, null, null, true },
                    { 2, null, null, null, null, true },
                    { 3, null, null, null, null, true },
                    { 4, null, null, null, null, true },
                    { 5, null, null, null, null, true },
                    { 6, null, null, null, null, true },
                    { 7, null, null, null, null, true },
                    { 8, null, null, null, null, true },
                    { 9, null, null, null, null, true },
                    { 10, null, null, null, null, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SortingBatchQueues_AssignedPalletId",
                schema: "sorting",
                table: "SortingBatchQueues",
                column: "AssignedPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingBatchQueues_Status_QueuedAt",
                schema: "sorting",
                table: "SortingBatchQueues",
                columns: new[] { "Status", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_PackingId",
                schema: "sorting",
                table: "SortingPalletPacks",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_PalletId",
                schema: "sorting",
                table: "SortingPalletPacks",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_Status_ScheduledAt",
                schema: "sorting",
                table: "SortingPalletPacks",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SortingStations_CurrentPalletId",
                schema: "sorting",
                table: "SortingStations",
                column: "CurrentPalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SortingBatchQueues",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortingPalletPacks",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortingStations",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "StationAuditLogs",
                schema: "sorting");

            migrationBuilder.DropColumn(
                name: "StationId",
                schema: "sorting",
                table: "SortingPallets");
        }
    }
}
