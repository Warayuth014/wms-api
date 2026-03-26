using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class MoveLotExpiredToParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiredDate",
                schema: "flow1",
                table: "POItems");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                schema: "flow1",
                table: "POItems");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiredDate",
                schema: "master",
                table: "Parts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                schema: "master",
                table: "Parts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiredDate",
                schema: "master",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                schema: "master",
                table: "Parts");

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
        }
    }
}
