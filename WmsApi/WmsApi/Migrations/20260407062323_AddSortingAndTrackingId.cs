using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSortingAndTrackingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sorting");

            migrationBuilder.AddColumn<string>(
                name: "TrackingId",
                schema: "unload",
                table: "Pallets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SortStations",
                schema: "sorting",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortStations", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "SortSessions",
                schema: "sorting",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SortPalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_SortSessions_Pallets_SortPalletId",
                        column: x => x.SortPalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_SortSessions_SortStations_StationId",
                        column: x => x.StationId,
                        principalSchema: "sorting",
                        principalTable: "SortStations",
                        principalColumn: "StationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SortSessionItems",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    SourcePalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    TrackingId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortSessionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SortSessionItems_Pallets_SourcePalletId",
                        column: x => x.SourcePalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_SortSessionItems_SortSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "sorting",
                        principalTable: "SortSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SortSessionItems_SessionId",
                schema: "sorting",
                table: "SortSessionItems",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SortSessionItems_SourcePalletId",
                schema: "sorting",
                table: "SortSessionItems",
                column: "SourcePalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortSessions_SortPalletId",
                schema: "sorting",
                table: "SortSessions",
                column: "SortPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortSessions_StationId",
                schema: "sorting",
                table: "SortSessions",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SortSessionItems",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortSessions",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortStations",
                schema: "sorting");

            migrationBuilder.DropColumn(
                name: "TrackingId",
                schema: "unload",
                table: "Pallets");
        }
    }
}
