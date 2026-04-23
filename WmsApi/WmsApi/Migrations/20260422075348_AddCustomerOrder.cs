using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer");

            migrationBuilder.AddColumn<string>(
                name: "CustomerOrderId",
                schema: "picking",
                table: "PickOrders",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerOrders",
                schema: "customer",
                columns: table => new
                {
                    CustomerOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrders", x => x.CustomerOrderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickOrders_CustomerOrderId",
                schema: "picking",
                table: "PickOrders",
                column: "CustomerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSlots_CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots",
                column: "CustomerOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckInSlots_CustomerOrders_CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots",
                column: "CustomerOrderId",
                principalSchema: "customer",
                principalTable: "CustomerOrders",
                principalColumn: "CustomerOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PickOrders_CustomerOrders_CustomerOrderId",
                schema: "picking",
                table: "PickOrders",
                column: "CustomerOrderId",
                principalSchema: "customer",
                principalTable: "CustomerOrders",
                principalColumn: "CustomerOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckInSlots_CustomerOrders_CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_PickOrders_CustomerOrders_CustomerOrderId",
                schema: "picking",
                table: "PickOrders");

            migrationBuilder.DropTable(
                name: "CustomerOrders",
                schema: "customer");

            migrationBuilder.DropIndex(
                name: "IX_PickOrders_CustomerOrderId",
                schema: "picking",
                table: "PickOrders");

            migrationBuilder.DropIndex(
                name: "IX_CheckInSlots_CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots");

            migrationBuilder.DropColumn(
                name: "CustomerOrderId",
                schema: "picking",
                table: "PickOrders");

            migrationBuilder.DropColumn(
                name: "CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots");
        }
    }
}
