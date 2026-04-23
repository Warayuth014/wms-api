using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPartSerials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartSerials",
                schema: "master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SerialNo = table.Column<string>(type: "nvarchar(80)", nullable: false),
                    ReceiptLineId = table.Column<int>(type: "int", nullable: true),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    PackedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartSerials_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                    table.ForeignKey(
                        name: "FK_PartSerials_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_PartSerials_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartSerials_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalSchema: "receiving",
                        principalTable: "ReceiptLines",
                        principalColumn: "LineId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PackingId",
                schema: "master",
                table: "PartSerials",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PalletId",
                schema: "master",
                table: "PartSerials",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PartId_SerialNo",
                schema: "master",
                table: "PartSerials",
                columns: new[] { "PartId", "SerialNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_ReceiptLineId",
                schema: "master",
                table: "PartSerials",
                column: "ReceiptLineId");

            // ── Backfill serials for existing stock ──
            migrationBuilder.Sql(@"
;WITH N AS (
    SELECT TOP (10000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
Expanded AS (
    SELECT rl.LineId, rl.PartId, rl.PalletId, rl.Status, N.n AS UnitIdx
    FROM receiving.ReceiptLines rl
    INNER JOIN N ON N.n <= rl.QtyReceived
    WHERE rl.QtyReceived > 0
),
Seq AS (
    SELECT e.*,
           ROW_NUMBER() OVER (PARTITION BY PartId ORDER BY LineId, UnitIdx) AS SerialSeq
    FROM Expanded e
)
INSERT INTO master.PartSerials (PartId, SerialNo, ReceiptLineId, PalletId, Status, CreatedAt, UpdatedAt)
SELECT
    PartId,
    CONCAT('SN-', PartId, '-', RIGHT('000000' + CAST(SerialSeq AS VARCHAR(10)), 6)),
    LineId,
    PalletId,
    'STORED',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM Seq;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartSerials",
                schema: "master");
        }
    }
}
