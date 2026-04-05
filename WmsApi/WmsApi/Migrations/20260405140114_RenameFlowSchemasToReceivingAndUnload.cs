using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameFlowSchemasToReceivingAndUnload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "unload");

            migrationBuilder.EnsureSchema(
                name: "receiving");

            migrationBuilder.RenameTable(
                name: "UnloadSessions",
                schema: "flow2",
                newName: "UnloadSessions",
                newSchema: "unload");

            migrationBuilder.RenameTable(
                name: "UnloadLines",
                schema: "flow2",
                newName: "UnloadLines",
                newSchema: "unload");

            migrationBuilder.RenameTable(
                name: "ReceivingSessions",
                schema: "flow1",
                newName: "ReceivingSessions",
                newSchema: "receiving");

            migrationBuilder.RenameTable(
                name: "ReceiptLines",
                schema: "flow1",
                newName: "ReceiptLines",
                newSchema: "receiving");

            migrationBuilder.RenameTable(
                name: "PurchaseOrders",
                schema: "flow1",
                newName: "PurchaseOrders",
                newSchema: "receiving");

            migrationBuilder.RenameTable(
                name: "POItems",
                schema: "flow1",
                newName: "POItems",
                newSchema: "receiving");

            migrationBuilder.RenameTable(
                name: "Pallets",
                schema: "flow2",
                newName: "Pallets",
                newSchema: "unload");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "flow2");

            migrationBuilder.EnsureSchema(
                name: "flow1");

            migrationBuilder.RenameTable(
                name: "UnloadSessions",
                schema: "unload",
                newName: "UnloadSessions",
                newSchema: "flow2");

            migrationBuilder.RenameTable(
                name: "UnloadLines",
                schema: "unload",
                newName: "UnloadLines",
                newSchema: "flow2");

            migrationBuilder.RenameTable(
                name: "ReceivingSessions",
                schema: "receiving",
                newName: "ReceivingSessions",
                newSchema: "flow1");

            migrationBuilder.RenameTable(
                name: "ReceiptLines",
                schema: "receiving",
                newName: "ReceiptLines",
                newSchema: "flow1");

            migrationBuilder.RenameTable(
                name: "PurchaseOrders",
                schema: "receiving",
                newName: "PurchaseOrders",
                newSchema: "flow1");

            migrationBuilder.RenameTable(
                name: "POItems",
                schema: "receiving",
                newName: "POItems",
                newSchema: "flow1");

            migrationBuilder.RenameTable(
                name: "Pallets",
                schema: "unload",
                newName: "Pallets",
                newSchema: "flow2");
        }
    }
}
