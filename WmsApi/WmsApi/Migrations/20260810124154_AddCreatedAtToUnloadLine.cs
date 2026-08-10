using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtToUnloadLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "unload",
                table: "UnloadLines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "unload",
                table: "UnloadLines");
        }
    }
}
