using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPart001PwLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PART001 เดิมมีแค่ line เดียว (FG) — เพิ่มอีก line เป็น PW เพื่อทดสอบ
            // popup เลือก condition (FG/PW) ตอนสแกน Part ID ฝั่ง Flutter
            migrationBuilder.Sql("""
DECLARE @NewPOItemId INT;

INSERT INTO receiving.POItems (POId, PartId, QtyOrdered, QtyReceived, QtyRemaining, Status, Condition, ExpiredDate)
VALUES ('PO001', 'PART001', 15, 0, 15, 'PENDING', 'PW', NULL);

SET @NewPOItemId = SCOPE_IDENTITY();

INSERT INTO receiving.POItemLots (POItemId, LotNumber, QtyOrdered)
VALUES (@NewPOItemId, 'LOT-A003', 15);

-- gen serial เพิ่มให้ PW line (PART001.SerialRequire=1) ต่อ sequence จากที่มีอยู่เดิม
;WITH Numbers AS (
    SELECT TOP (15) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
),
ExistingCount AS (
    SELECT COUNT(*) AS Cnt FROM master.PartSerials WHERE PartId = 'PART001'
)
INSERT INTO master.PartSerials (PartId, SerialNo, POItemId, ReceiptLineId, PalletId, Status, PackingId, PackedAt, CreatedAt, UpdatedAt)
SELECT
    'PART001',
    CONCAT('SN-PART001-', RIGHT('000000' + CAST(ec.Cnt + Numbers.n AS VARCHAR(10)), 6)),
    @NewPOItemId,
    NULL, NULL, 'STORED', NULL, NULL, GETUTCDATE(), GETUTCDATE()
FROM Numbers, ExistingCount ec;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DELETE FROM master.PartSerials
WHERE PartId = 'PART001'
  AND POItemId IN (SELECT Id FROM receiving.POItems WHERE PartId = 'PART001' AND Condition = 'PW');

DELETE FROM receiving.POItemLots
WHERE POItemId IN (SELECT Id FROM receiving.POItems WHERE PartId = 'PART001' AND Condition = 'PW');

DELETE FROM receiving.POItems WHERE PartId = 'PART001' AND Condition = 'PW';
""");
        }
    }
}
