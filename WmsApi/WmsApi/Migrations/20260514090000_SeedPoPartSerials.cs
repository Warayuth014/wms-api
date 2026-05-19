using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WmsApi.Data;

#nullable disable

namespace WmsApi.Migrations;

[Migration("20260514090000_SeedPoPartSerials")]
[DbContext(typeof(WmsDbContext))]
public partial class SeedPoPartSerials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
;WITH N AS (
    SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
NeededSerials AS (
    SELECT
        i.POId,
        i.PartId,
        N.n,
        CONCAT('SN-', i.POId, '-', i.PartId, '-', RIGHT('000000' + CAST(N.n AS VARCHAR(10)), 6)) AS SerialNo
    FROM receiving.POItems i
    INNER JOIN N ON N.n <= i.QtyOrdered
    WHERE i.QtyOrdered > 0
)
INSERT INTO master.PartSerials (PartId, SerialNo, ReceiptLineId, PalletId, Status, PackingId, PackedAt, CreatedAt, UpdatedAt)
SELECT
    ns.PartId,
    ns.SerialNo,
    NULL,
    NULL,
    'AVAILABLE',
    NULL,
    NULL,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM NeededSerials ns
WHERE NOT EXISTS (
    SELECT 1
    FROM master.PartSerials ps
    WHERE ps.PartId = ns.PartId
      AND ps.SerialNo = ns.SerialNo
);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DELETE ps
FROM master.PartSerials ps
WHERE ps.ReceiptLineId IS NULL
  AND ps.PalletId IS NULL
  AND ps.PackingId IS NULL
  AND ps.SerialNo LIKE 'SN-PO-%';
""");
    }
}
