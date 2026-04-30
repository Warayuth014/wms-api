using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSortingPallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sorting");

            migrationBuilder.AddColumn<DateTime>(
                name: "SortedAt",
                schema: "packing",
                table: "Packings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SortingPalletId",
                schema: "packing",
                table: "Packings",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightGram",
                schema: "packing",
                table: "Packings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SortingPallets",
                schema: "sorting",
                columns: table => new
                {
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CartonsCount = table.Column<int>(type: "int", nullable: false),
                    MaxCapacity = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SealedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingPallets", x => x.PalletId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packings_SortingPalletId",
                schema: "packing",
                table: "Packings",
                column: "SortingPalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packings_SortingPallets_SortingPalletId",
                schema: "packing",
                table: "Packings",
                column: "SortingPalletId",
                principalSchema: "sorting",
                principalTable: "SortingPallets",
                principalColumn: "PalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packings_SortingPallets_SortingPalletId",
                schema: "packing",
                table: "Packings");

            migrationBuilder.DropTable(
                name: "SortingPallets",
                schema: "sorting");

            migrationBuilder.DropIndex(
                name: "IX_Packings_SortingPalletId",
                schema: "packing",
                table: "Packings");

            migrationBuilder.DropColumn(
                name: "SortedAt",
                schema: "packing",
                table: "Packings");

            migrationBuilder.DropColumn(
                name: "SortingPalletId",
                schema: "packing",
                table: "Packings");

            migrationBuilder.DropColumn(
                name: "WeightGram",
                schema: "packing",
                table: "Packings");
        }
    }
}
