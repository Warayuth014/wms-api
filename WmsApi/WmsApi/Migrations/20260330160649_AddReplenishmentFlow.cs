using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReplenishmentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxStock",
                schema: "master",
                table: "Parts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinStock",
                schema: "master",
                table: "Parts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReplenishOrders",
                schema: "flow2",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishOrders", x => x.OrderId);
                });

            migrationBuilder.CreateTable(
                name: "Totes",
                schema: "master",
                columns: table => new
                {
                    ToteId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Totes", x => x.ToteId);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishOrderLines",
                schema: "flow2",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    QtyRequired = table.Column<int>(type: "int", nullable: false),
                    QtyFilled = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishOrderLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_ReplenishOrderLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishOrderLines_ReplenishOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "flow2",
                        principalTable: "ReplenishOrders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishSessions",
                schema: "flow2",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ToteId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ReplenishSessions_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishSessions_ReplenishOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "flow2",
                        principalTable: "ReplenishOrders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishSessions_Totes_ToteId",
                        column: x => x.ToteId,
                        principalSchema: "master",
                        principalTable: "Totes",
                        principalColumn: "ToteId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishSessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToteInventory",
                schema: "master",
                columns: table => new
                {
                    InventoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToteId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    QtyOnHand = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToteInventory", x => x.InventoryId);
                    table.ForeignKey(
                        name: "FK_ToteInventory_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToteInventory_Totes_ToteId",
                        column: x => x.ToteId,
                        principalSchema: "master",
                        principalTable: "Totes",
                        principalColumn: "ToteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishSessionLines",
                schema: "flow2",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    OrderLineId = table.Column<int>(type: "int", nullable: false),
                    QtyFilled = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishSessionLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_ReplenishSessionLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishSessionLines_ReplenishOrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "flow2",
                        principalTable: "ReplenishOrderLines",
                        principalColumn: "LineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReplenishSessionLines_ReplenishSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "flow2",
                        principalTable: "ReplenishSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishOrderLines_OrderId",
                schema: "flow2",
                table: "ReplenishOrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishOrderLines_PartId",
                schema: "flow2",
                table: "ReplenishOrderLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessionLines_OrderLineId",
                schema: "flow2",
                table: "ReplenishSessionLines",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessionLines_PartId",
                schema: "flow2",
                table: "ReplenishSessionLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessionLines_SessionId",
                schema: "flow2",
                table: "ReplenishSessionLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessions_OperatorId",
                schema: "flow2",
                table: "ReplenishSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessions_OrderId",
                schema: "flow2",
                table: "ReplenishSessions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessions_PalletId",
                schema: "flow2",
                table: "ReplenishSessions",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishSessions_ToteId",
                schema: "flow2",
                table: "ReplenishSessions",
                column: "ToteId");

            migrationBuilder.CreateIndex(
                name: "IX_ToteInventory_PartId",
                schema: "master",
                table: "ToteInventory",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ToteInventory_ToteId_PartId",
                schema: "master",
                table: "ToteInventory",
                columns: new[] { "ToteId", "PartId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplenishSessionLines",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "ToteInventory",
                schema: "master");

            migrationBuilder.DropTable(
                name: "ReplenishOrderLines",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "ReplenishSessions",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "ReplenishOrders",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "Totes",
                schema: "master");

            migrationBuilder.DropColumn(
                name: "MaxStock",
                schema: "master",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "MinStock",
                schema: "master",
                table: "Parts");
        }
    }
}
