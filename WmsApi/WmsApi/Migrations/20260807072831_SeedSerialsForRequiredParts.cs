using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedSerialsForRequiredParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Serial no. รูปแบบเดียวกับ ReceivingService.GenerateSerialsAsync: SN-{PartId}-{seq:D6}
            // seq ต่อจากจำนวน PartSerials ที่มีอยู่แล้วของ Part นั้น (กันชนกับที่ runtime gen เพิ่มทีหลัง)
            migrationBuilder.Sql("""
;WITH Numbers AS (
    SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
ExistingCounts AS (
    SELECT PartId, COUNT(*) AS Cnt FROM master.PartSerials GROUP BY PartId
),
NeededSerials AS (
    SELECT
        i.PartId,
        ISNULL(ec.Cnt, 0) + ROW_NUMBER() OVER (PARTITION BY i.PartId ORDER BY i.POId, Numbers.n) AS SeqNo
    FROM receiving.POItems i
    INNER JOIN master.Parts p ON p.PartId = i.PartId AND p.SerialRequire = 1
    INNER JOIN Numbers ON Numbers.n <= i.QtyOrdered
    LEFT JOIN ExistingCounts ec ON ec.PartId = i.PartId
)
INSERT INTO master.PartSerials (PartId, SerialNo, ReceiptLineId, PalletId, Status, PackingId, PackedAt, CreatedAt, UpdatedAt)
SELECT
    ns.PartId,
    CONCAT('SN-', ns.PartId, '-', RIGHT('000000' + CAST(ns.SeqNo AS VARCHAR(10)), 6)),
    NULL, NULL, 'STORED', NULL, NULL, GETUTCDATE(), GETUTCDATE()
FROM NeededSerials ns
WHERE NOT EXISTS (
    SELECT 1 FROM master.PartSerials ps
    WHERE ps.PartId = ns.PartId
      AND ps.SerialNo = CONCAT('SN-', ns.PartId, '-', RIGHT('000000' + CAST(ns.SeqNo AS VARCHAR(10)), 6))
);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DELETE ps
FROM master.PartSerials ps
INNER JOIN master.Parts p ON p.PartId = ps.PartId
WHERE p.SerialRequire = 1
  AND ps.ReceiptLineId IS NULL
  AND ps.PalletId IS NULL
  AND ps.PackingId IS NULL
  AND ps.SerialNo LIKE 'SN-' + ps.PartId + '-%';
""");
        }
    }
}
