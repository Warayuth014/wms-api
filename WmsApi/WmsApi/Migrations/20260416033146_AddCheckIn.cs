using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckInSlots",
                schema: "packing",
                columns: table => new
                {
                    SlotId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    TrackingId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInSlots", x => x.SlotId);
                });

            migrationBuilder.CreateTable(
                name: "CheckInEntries",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInEntries_CheckInSlots_SlotId",
                        column: x => x.SlotId,
                        principalSchema: "packing",
                        principalTable: "CheckInSlots",
                        principalColumn: "SlotId");
                    table.ForeignKey(
                        name: "FK_CheckInEntries_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInEntries_PackingId",
                schema: "packing",
                table: "CheckInEntries",
                column: "PackingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInEntries_SlotId",
                schema: "packing",
                table: "CheckInEntries",
                column: "SlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckInEntries",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "CheckInSlots",
                schema: "packing");
        }
    }
}
