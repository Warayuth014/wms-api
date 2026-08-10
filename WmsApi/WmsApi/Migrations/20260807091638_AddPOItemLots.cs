using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPOItemLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "POItemLots",
                schema: "receiving",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POItemId = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QtyOrdered = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POItemLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POItemLots_POItems_POItemId",
                        column: x => x.POItemId,
                        principalSchema: "receiving",
                        principalTable: "POItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Data migration: ก๊อป LotNumber เดิมของแต่ละ POItem มาเป็น POItemLot 1 แถว (lot เดียว เต็มจำนวน)
            // ก่อน column LotNumber บน POItems จะถูกลบทิ้ง
            migrationBuilder.Sql("""
INSERT INTO receiving.POItemLots (POItemId, LotNumber, QtyOrdered)
SELECT Id, LotNumber, QtyOrdered
FROM receiving.POItems
WHERE LotNumber IS NOT NULL;
""");

            // ตัวอย่าง: PART001 (POItem.Id=1) รับจริงมาจาก 2 lot — แก้จาก 1 แถว (50) เป็น 2 แถว (30+20)
            migrationBuilder.Sql("""
UPDATE receiving.POItemLots
SET QtyOrdered = 30
WHERE POItemId = 1 AND LotNumber = 'LOT-A001';

INSERT INTO receiving.POItemLots (POItemId, LotNumber, QtyOrdered)
VALUES (1, 'LOT-A002', 20);
""");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                schema: "receiving",
                table: "POItems");

            migrationBuilder.CreateIndex(
                name: "IX_POItemLots_POItemId_LotNumber",
                schema: "receiving",
                table: "POItemLots",
                columns: new[] { "POItemId", "LotNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POItemLots",
                schema: "receiving");

            migrationBuilder.DropIndex(
                name: "IX_POItems_POId_PartId",
                schema: "receiving",
                table: "POItems");

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                schema: "receiving",
                table: "POItems",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_POItems_POId_PartId_LotNumber",
                schema: "receiving",
                table: "POItems",
                columns: new[] { "POId", "PartId", "LotNumber" },
                unique: true,
                filter: "[LotNumber] IS NOT NULL");
        }
    }
}
