using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPacking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "packing");

            migrationBuilder.CreateTable(
                name: "Packings",
                schema: "packing",
                columns: table => new
                {
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrackingId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packings", x => x.PackingId);
                    table.ForeignKey(
                        name: "FK_Packings_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "PackingDetails",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackingDetails_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackingDetails_PickOrders_PickOrderId",
                        column: x => x.PickOrderId,
                        principalSchema: "picking",
                        principalTable: "PickOrders",
                        principalColumn: "PickOrderId");
                });

            migrationBuilder.CreateTable(
                name: "PackingPartScans",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedQty = table.Column<int>(type: "int", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingPartScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackingPartScans_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackingDetails_PackingId",
                schema: "packing",
                table: "PackingDetails",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_PackingDetails_PickOrderId",
                schema: "packing",
                table: "PackingDetails",
                column: "PickOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PackingPartScans_PackingId",
                schema: "packing",
                table: "PackingPartScans",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_Packings_PalletId",
                schema: "packing",
                table: "Packings",
                column: "PalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackingDetails",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PackingPartScans",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "Packings",
                schema: "packing");
        }
    }
}
