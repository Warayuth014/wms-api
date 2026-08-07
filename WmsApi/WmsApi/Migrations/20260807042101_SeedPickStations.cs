using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedPickStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "picking",
                table: "PickStations",
                columns: new[] { "StationId", "CurrentPalletId", "Name" },
                values: new object[,]
                {
                    { "STN-001", null, "Pick Station 01" },
                    { "STN-002", null, "Pick Station 02" },
                    { "STN-003", null, "Pick Station 03" },
                    { "STN-004", null, "Pick Station 04" },
                    { "STN-005", null, "Pick Station 05" },
                    { "STN-006", null, "Pick Station 06" },
                    { "STN-007", null, "Pick Station 07" },
                    { "STN-008", null, "Pick Station 08" },
                    { "STN-009", null, "Pick Station 09" },
                    { "STN-010", null, "Pick Station 10" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-001");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-002");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-003");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-004");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-005");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-006");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-007");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-008");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-009");

            migrationBuilder.DeleteData(
                schema: "picking",
                table: "PickStations",
                keyColumn: "StationId",
                keyValue: "STN-010");
        }
    }
}
