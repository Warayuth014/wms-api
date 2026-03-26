using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SplitPickOrderTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "PickOrderDetails",
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
                    table.PrimaryKey("PK_PickOrderDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickOrderDetails_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickOrderDetails_PickOrders_PickOrderId",
                        column: x => x.PickOrderId,
                        principalSchema: "picking",
                        principalTable: "PickOrders",
                        principalColumn: "PickOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PickOrderSubs",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickOrderDetailId = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<int>(type: "int", nullable: false),
                    AllocatedQty = table.Column<int>(type: "int", nullable: false),
                    PickedQty = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrderSubs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickOrderSubs_PickOrderDetails_PickOrderDetailId",
                        column: x => x.PickOrderDetailId,
                        principalSchema: "picking",
                        principalTable: "PickOrderDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickOrderSubs_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalSchema: "flow1",
                        principalTable: "ReceiptLines",
                        principalColumn: "LineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderDetails_PartId",
                schema: "picking",
                table: "PickOrderDetails",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderDetails_PickOrderId",
                schema: "picking",
                table: "PickOrderDetails",
                column: "PickOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderSubs_PickOrderDetailId",
                schema: "picking",
                table: "PickOrderSubs",
                column: "PickOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderSubs_ReceiptLineId",
                schema: "picking",
                table: "PickOrderSubs",
                column: "ReceiptLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickOrderSubs",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "PickOrderDetails",
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
    }
}
