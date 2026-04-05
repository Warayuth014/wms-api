using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyPickingSessionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PickingLines",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "PickingSessions",
                schema: "picking");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickingSessions",
                schema: "picking",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PackPalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_PickingSessions_Pallets_PackPalletId",
                        column: x => x.PackPalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingSessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PickingLines",
                schema: "picking",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperatorId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    BasketId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QtyPicked = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickingLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_PickingLines_Pallets_PickPalletId",
                        column: x => x.PickPalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickingLines_PickingSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "picking",
                        principalTable: "PickingSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickingLines_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickingLines_OperatorId",
                schema: "picking",
                table: "PickingLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingLines_PartId",
                schema: "picking",
                table: "PickingLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingLines_PickPalletId",
                schema: "picking",
                table: "PickingLines",
                column: "PickPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingLines_SessionId",
                schema: "picking",
                table: "PickingLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingSessions_OperatorId",
                schema: "picking",
                table: "PickingSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PickingSessions_PackPalletId",
                schema: "picking",
                table: "PickingSessions",
                column: "PackPalletId");
        }
    }
}
