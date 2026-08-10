using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialRequireToPart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SerialRequire",
                schema: "master",
                table: "Parts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ตัวอย่าง: สลับ true/false ให้ทดสอบทั้งสองเคสได้ (PART001/003/005 ต้องมี Serial)
            migrationBuilder.Sql("""
UPDATE master.Parts SET SerialRequire = 1 WHERE PartId IN ('PART001', 'PART003', 'PART005');
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialRequire",
                schema: "master",
                table: "Parts");
        }
    }
}
