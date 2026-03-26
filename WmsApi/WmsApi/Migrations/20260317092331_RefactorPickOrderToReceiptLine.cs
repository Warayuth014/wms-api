using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPickOrderToReceiptLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickOrderItems",
                schema: "picking");

            migrationBuilder.AddColumn<string>(
                name: "PickOrderId",
                schema: "flow1",
                table: "ReceiptLines",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickRequiredQty",
                schema: "flow1",
                table: "ReceiptLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PickReservedQty",
                schema: "flow1",
                table: "ReceiptLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_PickOrderId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "PickOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_PickOrders_PickOrderId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "PickOrderId",
                principalSchema: "picking",
                principalTable: "PickOrders",
                principalColumn: "PickOrderId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_PickOrders_PickOrderId",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_PickOrderId",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "PickOrderId",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "PickRequiredQty",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "PickReservedQty",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.CreateTable(
                name: "PickOrderItems",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
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
        }
    }
}
