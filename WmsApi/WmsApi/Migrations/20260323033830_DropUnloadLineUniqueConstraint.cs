using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class DropUnloadLineUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ลบ unique constraint (อาจเป็น constraint หรือ index)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_UnloadLine_SessionPart' AND parent_object_id = OBJECT_ID('flow2.UnloadLines'))
                    ALTER TABLE [flow2].[UnloadLines] DROP CONSTRAINT [UQ_UnloadLine_SessionPart];
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UnloadLines_SessionId_PartId' AND object_id = OBJECT_ID('flow2.UnloadLines'))
                    DROP INDEX [IX_UnloadLines_SessionId_PartId] ON [flow2].[UnloadLines];
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_SessionId",
                schema: "flow2",
                table: "UnloadLines",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnloadLines_SessionId",
                schema: "flow2",
                table: "UnloadLines");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_SessionId_PartId",
                schema: "flow2",
                table: "UnloadLines",
                columns: new[] { "SessionId", "PartId" },
                unique: true);
        }
    }
}
