using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class BaselineCurrentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "unload");

            migrationBuilder.EnsureSchema(
                name: "master");

            migrationBuilder.EnsureSchema(
                name: "picking");

            migrationBuilder.EnsureSchema(
                name: "receiving");

            migrationBuilder.EnsureSchema(
                name: "putaway");

            migrationBuilder.CreateTable(
                name: "Pallets",
                schema: "unload",
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
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinStock = table.Column<int>(type: "int", nullable: true),
                    MaxStock = table.Column<int>(type: "int", nullable: true),
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
                name: "PickStations",
                schema: "picking",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickStations", x => x.StationId);
                    table.ForeignKey(
                        name: "FK_PickStations_Pallets_CurrentPalletId",
                        column: x => x.CurrentPalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PreworkCutLogs",
                schema: "putaway",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    StationId = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CutAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreworkCutLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreworkCutLogs_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreworkCutLogs_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
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
                name: "PickOrders",
                schema: "picking",
                columns: table => new
                {
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrders", x => x.PickOrderId);
                    table.ForeignKey(
                        name: "FK_PickOrders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "receiving",
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
                name: "PutawaySessions",
                schema: "putaway",
                columns: table => new
                {
                    PutawayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WrappingRequired = table.Column<bool>(type: "bit", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawaySessions", x => x.PutawayId);
                    table.ForeignKey(
                        name: "FK_PutawaySessions_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutawaySessions_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnloadSessions",
                schema: "unload",
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
                        principalSchema: "unload",
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
                name: "PickOrderDetails",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    RequiredQty = table.Column<int>(type: "int", nullable: false),
                    ReservedQty = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrderDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickOrderDetails_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PickOrderDetails_PickOrders_PickOrderId",
                        column: x => x.PickOrderId,
                        principalSchema: "picking",
                        principalTable: "PickOrders",
                        principalColumn: "PickOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "POItems",
                schema: "receiving",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QtyOrdered = table.Column<int>(type: "int", nullable: false),
                    QtyReceived = table.Column<int>(type: "int", nullable: false),
                    QtyRemaining = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true)
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
                        principalSchema: "receiving",
                        principalTable: "PurchaseOrders",
                        principalColumn: "POId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingSessions",
                schema: "receiving",
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
                        principalSchema: "receiving",
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
                        principalSchema: "unload",
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
                        principalSchema: "unload",
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

            migrationBuilder.CreateTable(
                name: "UnloadLines",
                schema: "unload",
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
                        principalSchema: "unload",
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
                        principalSchema: "unload",
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
                schema: "receiving",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    POId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    QtyReceived = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiredDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                        principalSchema: "unload",
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
                        principalSchema: "receiving",
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

            migrationBuilder.CreateTable(
                name: "PickOrderSubs",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickOrderDetailId = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<int>(type: "int", nullable: false),
                    AllocatedQty = table.Column<int>(type: "int", nullable: false),
                    PickedQty = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrderSubs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickOrderSubs_PickOrderDetails_PickOrderDetailId",
                        column: x => x.PickOrderDetailId,
                        principalSchema: "picking",
                        principalTable: "PickOrderDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickOrderSubs_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalSchema: "receiving",
                        principalTable: "ReceiptLines",
                        principalColumn: "LineId",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_PickOrderDetails_PartId",
                schema: "picking",
                table: "PickOrderDetails",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderDetails_PickOrderId",
                schema: "picking",
                table: "PickOrderDetails",
                column: "PickOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrders_CreatedBy",
                schema: "picking",
                table: "PickOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderSubs_PickOrderDetailId",
                schema: "picking",
                table: "PickOrderSubs",
                column: "PickOrderDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_PickOrderSubs_ReceiptLineId",
                schema: "picking",
                table: "PickOrderSubs",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PickStations_CurrentPalletId",
                schema: "picking",
                table: "PickStations",
                column: "CurrentPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_PartId",
                schema: "receiving",
                table: "POItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_POItems_POId_PartId",
                schema: "receiving",
                table: "POItems",
                columns: new[] { "POId", "PartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreworkCutLogs_PalletId",
                schema: "putaway",
                table: "PreworkCutLogs",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PreworkCutLogs_PartId",
                schema: "putaway",
                table: "PreworkCutLogs",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedBy",
                schema: "receiving",
                table: "PurchaseOrders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                schema: "receiving",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawaySessions_OperatorId",
                schema: "putaway",
                table: "PutawaySessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawaySessions_PalletId",
                schema: "putaway",
                table: "PutawaySessions",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_OperatorId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_PalletId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_PartId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_SessionId",
                schema: "receiving",
                table: "ReceiptLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_OperatorId",
                schema: "receiving",
                table: "ReceivingSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingSessions_POId",
                schema: "receiving",
                table: "ReceivingSessions",
                column: "POId");

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
                name: "IX_UnloadLines_OperatorId",
                schema: "unload",
                table: "UnloadLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_PalletId",
                schema: "unload",
                table: "UnloadLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_PartId",
                schema: "unload",
                table: "UnloadLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadLines_SessionId",
                schema: "unload",
                table: "UnloadLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadSessions_OperatorId",
                schema: "unload",
                table: "UnloadSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UnloadSessions_PalletId",
                schema: "unload",
                table: "UnloadSessions",
                column: "PalletId");

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
                name: "CancelLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "PickOrderSubs",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "PickStations",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "POItems",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "PreworkCutLogs",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "ShipXQueue",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "UnloadLines",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "WrappingSessions",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "PickOrderDetails",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "ReceiptLines",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "UnloadSessions",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "PutawaySessions",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "PickOrders",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "Parts",
                schema: "master");

            migrationBuilder.DropTable(
                name: "ReceivingSessions",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "Pallets",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "master");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "master");
        }
    }
}
