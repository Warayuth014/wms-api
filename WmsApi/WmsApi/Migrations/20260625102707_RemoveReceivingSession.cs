using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReceivingSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive: drop FK ทุก constraint ที่ใช้ SessionId (รองรับ schema drift จาก FK ชื่อต่างๆ)
            migrationBuilder.Sql(@"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql = @sql + N'ALTER TABLE [receiving].[ReceiptLines] DROP CONSTRAINT [' + fk.name + N'];' + CHAR(13)
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fc ON fk.object_id = fc.constraint_object_id
                WHERE fk.parent_object_id = OBJECT_ID('receiving.ReceiptLines')
                  AND COL_NAME(fc.parent_object_id, fc.parent_column_id) = 'SessionId';
                EXEC sp_executesql @sql;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReceiptLines_SessionId' AND object_id = OBJECT_ID('receiving.ReceiptLines'))
                    DROP INDEX [IX_ReceiptLines_SessionId] ON [receiving].[ReceiptLines];
                IF COL_LENGTH('receiving.ReceiptLines', 'SessionId') IS NOT NULL
                    ALTER TABLE [receiving].[ReceiptLines] DROP COLUMN [SessionId];
                IF OBJECT_ID('receiving.ReceivingSessions', 'U') IS NOT NULL
                    DROP TABLE [receiving].[ReceivingSessions];
            ");

            // ReceiptLines.POId อยู่แล้ว nvarchar(50) ตรงกับ PurchaseOrders.POId — ไม่ alter
            // (ถ้า alter ไป 450 จะไม่ match ทำ FK ไม่ผ่าน)

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_POId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "POId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_PurchaseOrders_POId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "POId",
                principalSchema: "receiving",
                principalTable: "PurchaseOrders",
                principalColumn: "POId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_PurchaseOrders_POId",
                schema: "receiving",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_POId",
                schema: "receiving",
                table: "ReceiptLines");

            migrationBuilder.AlterColumn<string>(
                name: "POId",
                schema: "receiving",
                table: "ReceiptLines",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                schema: "receiving",
                table: "ReceiptLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReceivingSessions",
                schema: "receiving",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ReceivingSessions_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalSchema: "receiving",
                        principalTable: "PurchaseOrders",
                        principalColumn: "POId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivingSessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_SessionId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_OperatorId",
                schema: "receiving",
                table: "ReceivingSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_POId",
                schema: "receiving",
                table: "ReceivingSessions",
                column: "POId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_ReceivingSessions_SessionId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "SessionId",
                principalSchema: "receiving",
                principalTable: "ReceivingSessions",
                principalColumn: "SessionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
