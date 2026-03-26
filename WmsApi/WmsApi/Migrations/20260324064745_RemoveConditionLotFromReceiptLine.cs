using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConditionLotFromReceiptLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "ExpiredDate",
                schema: "flow1",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                schema: "flow1",
                table: "ReceiptLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                schema: "flow1",
                table: "ReceiptLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiredDate",
                schema: "flow1",
                table: "ReceiptLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                schema: "flow1",
                table: "ReceiptLines",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
