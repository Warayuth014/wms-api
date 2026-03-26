using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class PickOrderFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickOrders",
                schema: "picking",
                columns: table => new
                {
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrders", x => x.PickOrderId);
                    table.ForeignKey(
                        name: "FK_PickOrders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PickStations",
                schema: "picking",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickStations", x => x.StationId);
                    table.ForeignKey(
                        name: "FK_PickStations_Pallets_CurrentPalletId",
                        column: x => x.CurrentPalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PickOrderItems",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    RequiredQty = table.Column<int>(type: "int", nullable: false),
                    ReservedQty = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickOrderItems_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickOrderItems_PickOrders_PickOrderId",
                        column: x => x.PickOrderId,
                        principalSchema: "picking",
                        principalTable: "PickOrders",
                        principalColumn: "PickOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderItems_PartId",
                schema: "picking",
                table: "PickOrderItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderItems_PickOrderId_PartId",
                schema: "picking",
                table: "PickOrderItems",
                columns: new[] { "PickOrderId", "PartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickOrders_CreatedBy",
                schema: "picking",
                table: "PickOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PickStations_CurrentPalletId",
                schema: "picking",
                table: "PickStations",
                column: "CurrentPalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickOrderItems",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "PickStations",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "PickOrders",
                schema: "picking");
        }
    }
}
