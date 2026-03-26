using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionToPartAndLotToPOItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiredDate",
                schema: "flow1",
                table: "POItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                schema: "flow1",
                table: "POItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                schema: "master",
                table: "Parts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiredDate",
                schema: "flow1",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                schema: "flow1",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "Condition",
                schema: "master",
                table: "Parts");
        }
    }
}
