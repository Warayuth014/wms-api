using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RenamePartSerialToPOItemLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartSerials_POItems_POItemId",
                schema: "master",
                table: "PartSerials");

            migrationBuilder.RenameColumn(
                name: "POItemId",
                schema: "master",
                table: "PartSerials",
                newName: "POItemLotId");

            migrationBuilder.RenameIndex(
                name: "IX_PartSerials_POItemId",
                schema: "master",
                table: "PartSerials",
                newName: "IX_PartSerials_POItemLotId");

            // ค่าที่ rename มา ตอนนี้ยังเป็น POItem.Id เดิม (ผิดความหมาย เพราะ FK เปลี่ยนไปชี้ POItemLots
            // แล้ว) ต้อง remap เป็น POItemLot.Id จริง — เรียง serial ตาม SerialNo แล้วแจกเข้าแต่ละ lot
            // ตามลำดับจนครบ QtyOrdered ของ lot นั้น ส่วนที่เกิน (over-received ตอนทดสอบ) ตกเป็นของ lot สุดท้าย
            migrationBuilder.Sql("""
;WITH LotRank AS (
    SELECT Id, POItemId,
           ROW_NUMBER() OVER (PARTITION BY POItemId ORDER BY Id) AS rn,
           SUM(QtyOrdered) OVER (PARTITION BY POItemId ORDER BY Id
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS CumQty,
           COUNT(*) OVER (PARTITION BY POItemId) AS LotCount
    FROM receiving.POItemLots
),
SerialRank AS (
    SELECT Id, POItemLotId AS OldPOItemId,
           ROW_NUMBER() OVER (PARTITION BY POItemLotId ORDER BY SerialNo) AS rn
    FROM master.PartSerials
    WHERE POItemLotId IS NOT NULL
)
UPDATE ps
SET ps.POItemLotId = COALESCE(
    (SELECT TOP 1 lr.Id FROM LotRank lr
     WHERE lr.POItemId = sr.OldPOItemId AND sr.rn <= lr.CumQty
     ORDER BY lr.rn),
    (SELECT TOP 1 lr.Id FROM LotRank lr
     WHERE lr.POItemId = sr.OldPOItemId
     ORDER BY lr.rn DESC)
)
FROM master.PartSerials ps
INNER JOIN SerialRank sr ON sr.Id = ps.Id;
""");

            migrationBuilder.AddForeignKey(
                name: "FK_PartSerials_POItemLots_POItemLotId",
                schema: "master",
                table: "PartSerials",
                column: "POItemLotId",
                principalSchema: "receiving",
                principalTable: "POItemLots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartSerials_POItemLots_POItemLotId",
                schema: "master",
                table: "PartSerials");

            migrationBuilder.RenameColumn(
                name: "POItemLotId",
                schema: "master",
                table: "PartSerials",
                newName: "POItemId");

            migrationBuilder.RenameIndex(
                name: "IX_PartSerials_POItemLotId",
                schema: "master",
                table: "PartSerials",
                newName: "IX_PartSerials_POItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartSerials_POItems_POItemId",
                schema: "master",
                table: "PartSerials",
                column: "POItemId",
                principalSchema: "receiving",
                principalTable: "POItems",
                principalColumn: "Id");
        }
    }
}
