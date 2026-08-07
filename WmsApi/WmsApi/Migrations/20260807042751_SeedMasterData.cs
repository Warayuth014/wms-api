using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
INSERT INTO master.Users (UserId, FullName, Role, IsActive, CreatedAt) VALUES
('USR-001', N'สมชาย ใจดี', 'OPERATOR', 1, GETUTCDATE()),
('USR-002', N'สมหญิง รักงาน', 'OPERATOR', 1, GETUTCDATE()),
('USR-003', N'สมศักดิ์ หัวหน้า', 'SUPERVISOR', 1, GETUTCDATE());

INSERT INTO master.Suppliers (SupplierId, ShortName, FullName, CreatedAt) VALUES
('SUP001', N'Thai Auto Parts', N'Thai Auto Parts Co., Ltd.', GETUTCDATE()),
('SUP002', N'Global Supply', N'Global Supply Chain Co., Ltd.', GETUTCDATE());

INSERT INTO master.Parts (PartId, Owner, Brand, ItemDesc, ImageUrl, MinStock, MaxStock, CreatedAt) VALUES
('PART001', 'TOYOTA', 'TOYOTA', N'Brake Pad Front Set', NULL, 10, 200, GETUTCDATE()),
('PART002', 'TOYOTA', 'TOYOTA', N'Oil Filter', NULL, 20, 300, GETUTCDATE()),
('PART003', 'HONDA', 'HONDA', N'Air Filter', NULL, 15, 250, GETUTCDATE()),
('PART004', 'HONDA', 'HONDA', N'Spark Plug', NULL, 30, 400, GETUTCDATE()),
('PART005', 'ISUZU', 'ISUZU', N'Clutch Disc', NULL, 5, 100, GETUTCDATE()),
('PART006', 'ISUZU', 'ISUZU', N'Timing Belt', NULL, 8, 150, GETUTCDATE());

-- 50 pallet เปล่าพร้อมใช้ (PLT001..PLT050) — Type=NULL รับได้ทั้ง FG/PW
INSERT INTO unload.Pallets (PalletId, Type, Status, Location, TrackingId, CreatedAt, UpdatedAt)
SELECT CONCAT('PLT', RIGHT('000' + CAST(n AS VARCHAR(3)), 3)), NULL, 'AVAILABLE', NULL, NULL, GETUTCDATE(), GETUTCDATE()
FROM (SELECT TOP (50) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) t;

INSERT INTO receiving.PurchaseOrders (POId, SupplierId, Status, CreatedBy, CreatedAt, UpdatedAt) VALUES
('PO001', 'SUP001', 'OPEN', 'USR-003', GETUTCDATE(), GETUTCDATE()),
('PO002', 'SUP001', 'OPEN', 'USR-003', GETUTCDATE(), GETUTCDATE()),
('PO003', 'SUP002', 'OPEN', 'USR-003', GETUTCDATE(), GETUTCDATE());

INSERT INTO receiving.POItems (POId, PartId, QtyOrdered, QtyReceived, QtyRemaining, Status, Condition, LotNumber, ExpiredDate) VALUES
('PO001', 'PART001', 50, 0, 50, 'PENDING', 'FG', 'LOT-A001', NULL),
('PO001', 'PART002', 100, 0, 100, 'PENDING', 'FG', 'LOT-A002', NULL),
('PO002', 'PART003', 60, 0, 60, 'PENDING', 'FG', 'LOT-B001', NULL),
('PO002', 'PART004', 80, 0, 80, 'PENDING', 'FG', 'LOT-B002', NULL),
('PO003', 'PART005', 30, 0, 30, 'PENDING', 'FG', 'LOT-C001', NULL),
('PO003', 'PART006', 40, 0, 40, 'PENDING', 'FG', 'LOT-C002', NULL);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DELETE FROM receiving.POItems WHERE POId IN ('PO001', 'PO002', 'PO003');
DELETE FROM receiving.PurchaseOrders WHERE POId IN ('PO001', 'PO002', 'PO003');
DELETE FROM unload.Pallets WHERE PalletId LIKE 'PLT0[0-9][0-9]';
DELETE FROM master.Parts WHERE PartId IN ('PART001', 'PART002', 'PART003', 'PART004', 'PART005', 'PART006');
DELETE FROM master.Suppliers WHERE SupplierId IN ('SUP001', 'SUP002');
DELETE FROM master.Users WHERE UserId IN ('USR-001', 'USR-002', 'USR-003');
""");
        }
    }
}
