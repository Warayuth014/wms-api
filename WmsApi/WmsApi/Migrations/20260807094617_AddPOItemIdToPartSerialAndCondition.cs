using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPOItemIdToPartSerialAndCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_POItems_POId_PartId",
                schema: "receiving",
                table: "POItems");

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                schema: "receiving",
                table: "POItems",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "POItemId",
                schema: "master",
                table: "PartSerials",
                type: "int",
                nullable: true);

            // Backfill: serial เดิมทั้งหมด → ผูกกับ POItem ของ Part นั้น (ตอนนี้ 1 Part = 1 POItem เสมอ)
            migrationBuilder.Sql("""
UPDATE ps
SET ps.POItemId = i.Id
FROM master.PartSerials ps
INNER JOIN receiving.POItems i ON i.PartId = ps.PartId
WHERE ps.POItemId IS NULL;
""");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_POId_PartId_Condition",
                schema: "receiving",
                table: "POItems",
                columns: new[] { "POId", "PartId", "Condition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_POItemId",
                schema: "master",
                table: "PartSerials",
                column: "POItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PartSerials_POItems_POItemId",
                schema: "master",
                table: "PartSerials",
                column: "POItemId",
                principalSchema: "receiving",
                principalTable: "POItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartSerials_POItems_POItemId",
                schema: "master",
                table: "PartSerials");

            migrationBuilder.DropIndex(
                name: "IX_POItems_POId_PartId_Condition",
                schema: "receiving",
                table: "POItems");

            migrationBuilder.DropIndex(
                name: "IX_PartSerials_POItemId",
                schema: "master",
                table: "PartSerials");

            migrationBuilder.DropColumn(
                name: "POItemId",
                schema: "master",
                table: "PartSerials");

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                schema: "receiving",
                table: "POItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_POId_PartId",
                schema: "receiving",
                table: "POItems",
                columns: new[] { "POId", "PartId" },
                unique: true);
        }
    }
}
