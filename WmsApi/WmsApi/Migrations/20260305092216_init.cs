using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "flow2");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "master");

            migrationBuilder.EnsureSchema(
                name: "flow1");

            migrationBuilder.CreateTable(
                name: "Baskets",
                schema: "flow2",
                columns: table => new
                {
                    BasketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baskets", x => x.BasketId);
                });

            migrationBuilder.CreateTable(
                name: "Pallets",
                schema: "flow2",
                columns: table => new
                {
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pallets", x => x.PalletId);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                schema: "master",
                columns: table => new
                {
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.PartId);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "master",
                columns: table => new
                {
                    SupplierId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.SupplierId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "master",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "CancelLog",
                schema: "audit",
                columns: table => new
                {
                    CancelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RefType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelLog", x => x.CancelId);
                    table.ForeignKey(
                        name: "FK_CancelLog_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancelLog_Users_RequestBy",
                        column: x => x.RequestBy,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "flow1",
                columns: table => new
                {
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.POId);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "master",
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnloadSessions",
                schema: "flow2",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Step1DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnloadSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_UnloadSessions_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnloadSessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "POItems",
                schema: "flow1",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QtyOrdered = table.Column<int>(type: "int", nullable: false),
                    QtyReceived = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POItems_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_POItems_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalSchema: "flow1",
                        principalTable: "PurchaseOrders",
                        principalColumn: "POId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingSessions",
                schema: "flow1",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ReceivingSessions_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalSchema: "flow1",
                        principalTable: "PurchaseOrders",
                        principalColumn: "POId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivingSessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BasketLines",
                schema: "flow2",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    BasketId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QtyLoaded = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_BasketLines_Baskets_BasketId",
                        column: x => x.BasketId,
                        principalSchema: "flow2",
                        principalTable: "Baskets",
                        principalColumn: "BasketId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BasketLines_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BasketLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BasketLines_UnloadSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "flow2",
                        principalTable: "UnloadSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BasketLines_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnloadLines",
                schema: "flow2",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QtyUnloaded = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnloadLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_UnloadLines_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnloadLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnloadLines_UnloadSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "flow2",
                        principalTable: "UnloadSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnloadLines_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                schema: "flow1",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    POId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    QtyReceived = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "flow2",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_ReceivingSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "flow1",
                        principalTable: "ReceivingSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_BasketId",
                schema: "flow2",
                table: "BasketLines",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_OperatorId",
                schema: "flow2",
                table: "BasketLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_PalletId",
                schema: "flow2",
                table: "BasketLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_PartId",
                schema: "flow2",
                table: "BasketLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_SessionId",
                schema: "flow2",
                table: "BasketLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CancelLog_ApprovedBy",
                schema: "audit",
                table: "CancelLog",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CancelLog_RequestBy",
                schema: "audit",
                table: "CancelLog",
                column: "RequestBy");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_PartId",
                schema: "flow1",
                table: "POItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_POId_PartId",
                schema: "flow1",
                table: "POItems",
                columns: new[] { "POId", "PartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedBy",
                schema: "flow1",
                table: "PurchaseOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                schema: "flow1",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_OperatorId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_PalletId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_PartId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_SessionId",
                schema: "flow1",
                table: "ReceiptLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_OperatorId",
                schema: "flow1",
                table: "ReceivingSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_POId",
                schema: "flow1",
                table: "ReceivingSessions",
                column: "POId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_OperatorId",
                schema: "flow2",
                table: "UnloadLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_PalletId",
                schema: "flow2",
                table: "UnloadLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_PartId",
                schema: "flow2",
                table: "UnloadLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_SessionId_PartId",
                schema: "flow2",
                table: "UnloadLines",
                columns: new[] { "SessionId", "PartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnloadSessions_OperatorId",
                schema: "flow2",
                table: "UnloadSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadSessions_PalletId",
                schema: "flow2",
                table: "UnloadSessions",
                column: "PalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketLines",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "CancelLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "POItems",
                schema: "flow1");

            migrationBuilder.DropTable(
                name: "ReceiptLines",
                schema: "flow1");

            migrationBuilder.DropTable(
                name: "UnloadLines",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "Baskets",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "ReceivingSessions",
                schema: "flow1");

            migrationBuilder.DropTable(
                name: "Parts",
                schema: "master");

            migrationBuilder.DropTable(
                name: "UnloadSessions",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "flow1");

            migrationBuilder.DropTable(
                name: "Pallets",
                schema: "flow2");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "master");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "master");
        }
    }
}
