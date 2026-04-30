using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class SyncPackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Packings: เพิ่ม column ถ้ายังไม่มี ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA='packing' AND TABLE_NAME='Packings' AND COLUMN_NAME='PickOrderId')
                ALTER TABLE packing.Packings ADD PickOrderId NVARCHAR(50) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA='packing' AND TABLE_NAME='Packings' AND COLUMN_NAME='SeqNo')
                ALTER TABLE packing.Packings ADD SeqNo INT NOT NULL DEFAULT 0;
            ");

            // ── PackingPartScans: เพิ่ม PickOrderId ที่ขาด ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA='packing' AND TABLE_NAME='PackingPartScans' AND COLUMN_NAME='PickOrderId')
                ALTER TABLE packing.PackingPartScans ADD PickOrderId NVARCHAR(50) NOT NULL DEFAULT '';
            ");

            // ── Baskets: สร้างถ้ายังไม่มี ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA='unload' AND TABLE_NAME='Baskets')
                BEGIN
                    CREATE TABLE unload.Baskets (
                        BasketId  NVARCHAR(50)  NOT NULL PRIMARY KEY,
                        Label     NVARCHAR(100) NOT NULL,
                        Zone      NVARCHAR(50)  NULL,
                        Destination NVARCHAR(100) NULL,
                        Status    NVARCHAR(30)  NOT NULL DEFAULT 'OPEN',
                        CreatedAt DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedAt DATETIME2     NOT NULL DEFAULT GETUTCDATE()
                    );
                END
            ");

            // ── BasketLines: สร้างถ้ายังไม่มี ──
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA='unload' AND TABLE_NAME='BasketLines')
                BEGIN
                    CREATE TABLE unload.BasketLines (
                        LineId       INT           IDENTITY(1,1) PRIMARY KEY,
                        SessionId    INT           NOT NULL,
                        UnloadLineId INT           NOT NULL,
                        BasketId     NVARCHAR(50)  NOT NULL,
                        PartId       NVARCHAR(50)  NOT NULL,
                        PalletId     NVARCHAR(50)  NOT NULL,
                        LotNumber    NVARCHAR(MAX) NULL,
                        ExpiredDate  DATE          NULL,
                        QtyLoaded    INT           NOT NULL DEFAULT 0,
                        Status       NVARCHAR(MAX) NOT NULL DEFAULT 'LOADED',
                        LoadedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                        OperatorId   NVARCHAR(50)  NOT NULL,
                        CONSTRAINT FK_BasketLines_Baskets_BasketId FOREIGN KEY (BasketId)
                            REFERENCES unload.Baskets(BasketId),
                        CONSTRAINT FK_BasketLines_Pallets_PalletId FOREIGN KEY (PalletId)
                            REFERENCES unload.Pallets(PalletId),
                        CONSTRAINT FK_BasketLines_Parts_PartId FOREIGN KEY (PartId)
                            REFERENCES master.Parts(PartId),
                        CONSTRAINT FK_BasketLines_UnloadLines_UnloadLineId FOREIGN KEY (UnloadLineId)
                            REFERENCES unload.UnloadLines(LineId),
                        CONSTRAINT FK_BasketLines_UnloadSessions_SessionId FOREIGN KEY (SessionId)
                            REFERENCES unload.UnloadSessions(SessionId),
                        CONSTRAINT FK_BasketLines_Users_OperatorId FOREIGN KEY (OperatorId)
                            REFERENCES master.Users(UserId)
                    );

                    CREATE INDEX IX_BasketLines_BasketId ON unload.BasketLines(BasketId);
                    CREATE INDEX IX_BasketLines_OperatorId ON unload.BasketLines(OperatorId);
                    CREATE INDEX IX_BasketLines_PalletId ON unload.BasketLines(PalletId);
                    CREATE INDEX IX_BasketLines_PartId ON unload.BasketLines(PartId);
                    CREATE INDEX IX_BasketLines_SessionId ON unload.BasketLines(SessionId);
                    CREATE INDEX IX_BasketLines_UnloadLineId ON unload.BasketLines(UnloadLineId);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketLines",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "Baskets",
                schema: "unload");

            migrationBuilder.DropColumn(
                name: "PickOrderId",
                schema: "packing",
                table: "Packings");

            migrationBuilder.DropColumn(
                name: "SeqNo",
                schema: "packing",
                table: "Packings");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA='packing' AND TABLE_NAME='PackingPartScans' AND COLUMN_NAME='PickOrderId')
                ALTER TABLE packing.PackingPartScans DROP COLUMN PickOrderId;
            ");
        }
    }
}
