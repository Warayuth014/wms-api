using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class DropToteAndToteInventoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToteInventory",
                schema: "master");

            migrationBuilder.DropTable(
                name: "Totes",
                schema: "master");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Totes",
                schema: "master",
                columns: table => new
                {
                    ToteId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Totes", x => x.ToteId);
                });

            migrationBuilder.CreateTable(
                name: "ToteInventory",
                schema: "master",
                columns: table => new
                {
                    InventoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ToteId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    QtyOnHand = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToteInventory", x => x.InventoryId);
                    table.ForeignKey(
                        name: "FK_ToteInventory_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToteInventory_Totes_ToteId",
                        column: x => x.ToteId,
                        principalSchema: "master",
                        principalTable: "Totes",
                        principalColumn: "ToteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToteInventory_PartId",
                schema: "master",
                table: "ToteInventory",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ToteInventory_ToteId_PartId",
                schema: "master",
                table: "ToteInventory",
                columns: new[] { "ToteId", "PartId" },
                unique: true);
        }
    }
}
