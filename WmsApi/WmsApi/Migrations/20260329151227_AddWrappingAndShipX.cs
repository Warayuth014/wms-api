using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddWrappingAndShipX : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WrappingRequired",
                schema: "putaway",
                table: "PutawaySessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShipXQueue",
                schema: "putaway",
                columns: table => new
                {
                    QueueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PutawayId = table.Column<int>(type: "int", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipXQueue", x => x.QueueId);
                    table.ForeignKey(
                        name: "FK_ShipXQueue_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipXQueue_PutawaySessions_PutawayId",
                        column: x => x.PutawayId,
                        principalSchema: "putaway",
                        principalTable: "PutawaySessions",
                        principalColumn: "PutawayId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WrappingSessions",
                schema: "putaway",
                columns: table => new
                {
                    WrappingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PutawayId = table.Column<int>(type: "int", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WrappingSessions", x => x.WrappingId);
                    table.ForeignKey(
                        name: "FK_WrappingSessions_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WrappingSessions_PutawaySessions_PutawayId",
                        column: x => x.PutawayId,
                        principalSchema: "putaway",
                        principalTable: "PutawaySessions",
                        principalColumn: "PutawayId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipXQueue_PalletId",
                schema: "putaway",
                table: "ShipXQueue",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipXQueue_PutawayId",
                schema: "putaway",
                table: "ShipXQueue",
                column: "PutawayId");

            migrationBuilder.CreateIndex(
                name: "IX_WrappingSessions_PalletId",
                schema: "putaway",
                table: "WrappingSessions",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WrappingSessions_PutawayId",
                schema: "putaway",
                table: "WrappingSessions",
                column: "PutawayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipXQueue",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "WrappingSessions",
                schema: "putaway");

            migrationBuilder.DropColumn(
                name: "WrappingRequired",
                schema: "putaway",
                table: "PutawaySessions");
        }
    }
}
