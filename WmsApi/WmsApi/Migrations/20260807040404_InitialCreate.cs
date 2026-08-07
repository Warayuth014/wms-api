using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WmsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "unload");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "packing");

            migrationBuilder.EnsureSchema(
                name: "customer");

            migrationBuilder.EnsureSchema(
                name: "master");

            migrationBuilder.EnsureSchema(
                name: "picking");

            migrationBuilder.EnsureSchema(
                name: "receiving");

            migrationBuilder.EnsureSchema(
                name: "putaway");

            migrationBuilder.EnsureSchema(
                name: "sorting");

            migrationBuilder.CreateTable(
                name: "Baskets",
                schema: "unload",
                columns: table => new
                {
                    BasketId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Baskets", x => x.BasketId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOrders",
                schema: "customer",
                columns: table => new
                {
                    CustomerOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrders", x => x.CustomerOrderId);
                });

            migrationBuilder.CreateTable(
                name: "Pallets",
                schema: "unload",
                columns: table => new
                {
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackingId = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "SortingPallets",
                schema: "sorting",
                columns: table => new
                {
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CartonsCount = table.Column<int>(type: "int", nullable: false),
                    MaxCapacity = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SealedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingPallets", x => x.PalletId);
                });

            migrationBuilder.CreateTable(
                name: "StationAuditLogs",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    OperatorId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", nullable: true),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationAuditLogs", x => x.Id);
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
                name: "CheckInSlots",
                schema: "packing",
                columns: table => new
                {
                    SlotId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    CustomerOrderId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInSlots", x => x.SlotId);
                    table.ForeignKey(
                        name: "FK_CheckInSlots_CustomerOrders_CustomerOrderId",
                        column: x => x.CustomerOrderId,
                        principalSchema: "customer",
                        principalTable: "CustomerOrders",
                        principalColumn: "CustomerOrderId");
                });

            migrationBuilder.CreateTable(
                name: "PickStations",
                schema: "picking",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPalletId = table.Column<string>(type: "nvarchar(450)", nullable: true)
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
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StationId = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                name: "Packings",
                schema: "packing",
                columns: table => new
                {
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    SeqNo = table.Column<int>(type: "int", nullable: false),
                    TrackingId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    SortingPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    WeightGram = table.Column<int>(type: "int", nullable: true),
                    SortedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packings", x => x.PackingId);
                    table.ForeignKey(
                        name: "FK_Packings_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_Packings_SortingPallets_SortingPalletId",
                        column: x => x.SortingPalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "SortingBatchQueues",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PackingIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    AssignedPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingBatchQueues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SortingBatchQueues_SortingPallets_AssignedPalletId",
                        column: x => x.AssignedPalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "SortingStations",
                schema: "sorting",
                columns: table => new
                {
                    StationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPalletId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    DisabledBy = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisableReason = table.Column<string>(type: "nvarchar(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingStations", x => x.StationId);
                    table.ForeignKey(
                        name: "FK_SortingStations_SortingPallets_CurrentPalletId",
                        column: x => x.CurrentPalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
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
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerOrderId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickOrders", x => x.PickOrderId);
                    table.ForeignKey(
                        name: "FK_PickOrders_CustomerOrders_CustomerOrderId",
                        column: x => x.CustomerOrderId,
                        principalSchema: "customer",
                        principalTable: "CustomerOrders",
                        principalColumn: "CustomerOrderId");
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
                name: "CheckInEntries",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckInEntries_CheckInSlots_SlotId",
                        column: x => x.SlotId,
                        principalSchema: "packing",
                        principalTable: "CheckInSlots",
                        principalColumn: "SlotId");
                    table.ForeignKey(
                        name: "FK_CheckInEntries_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                });

            migrationBuilder.CreateTable(
                name: "PackingPartScans",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedQty = table.Column<int>(type: "int", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingPartScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackingPartScans_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SortingPalletPacks",
                schema: "sorting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PalletId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortingPalletPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SortingPalletPacks_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                    table.ForeignKey(
                        name: "FK_SortingPalletPacks_SortingPallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "sorting",
                        principalTable: "SortingPallets",
                        principalColumn: "PalletId");
                });

            migrationBuilder.CreateTable(
                name: "PackingDetails",
                schema: "packing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackingDetails_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackingDetails_PickOrders_PickOrderId",
                        column: x => x.PickOrderId,
                        principalSchema: "picking",
                        principalTable: "PickOrders",
                        principalColumn: "PickOrderId");
                });

            migrationBuilder.CreateTable(
                name: "PickOrderDetails",
                schema: "picking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PickOrderId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                name: "ReceiptLines",
                schema: "receiving",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                        name: "FK_ReceiptLines_PurchaseOrders_POId",
                        column: x => x.POId,
                        principalSchema: "receiving",
                        principalTable: "PurchaseOrders",
                        principalColumn: "POId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Users_OperatorId",
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
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                name: "PartSerials",
                schema: "master",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SerialNo = table.Column<string>(type: "nvarchar(80)", nullable: false),
                    ReceiptLineId = table.Column<int>(type: "int", nullable: true),
                    PalletId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    PackingId = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    PackedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartSerials_Packings_PackingId",
                        column: x => x.PackingId,
                        principalSchema: "packing",
                        principalTable: "Packings",
                        principalColumn: "PackingId");
                    table.ForeignKey(
                        name: "FK_PartSerials_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_PartSerials_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartSerials_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalSchema: "receiving",
                        principalTable: "ReceiptLines",
                        principalColumn: "LineId");
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

            migrationBuilder.CreateTable(
                name: "BasketLines",
                schema: "unload",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    UnloadLineId = table.Column<int>(type: "int", nullable: false),
                    BasketId = table.Column<string>(type: "nvarchar(50)", nullable: false),
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
                        principalSchema: "unload",
                        principalTable: "Baskets",
                        principalColumn: "BasketId");
                    table.ForeignKey(
                        name: "FK_BasketLines_Pallets_PalletId",
                        column: x => x.PalletId,
                        principalSchema: "unload",
                        principalTable: "Pallets",
                        principalColumn: "PalletId");
                    table.ForeignKey(
                        name: "FK_BasketLines_Parts_PartId",
                        column: x => x.PartId,
                        principalSchema: "master",
                        principalTable: "Parts",
                        principalColumn: "PartId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BasketLines_UnloadLines_UnloadLineId",
                        column: x => x.UnloadLineId,
                        principalSchema: "unload",
                        principalTable: "UnloadLines",
                        principalColumn: "LineId");
                    table.ForeignKey(
                        name: "FK_BasketLines_UnloadSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "unload",
                        principalTable: "UnloadSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BasketLines_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "master",
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "sorting",
                table: "SortingStations",
                columns: new[] { "StationId", "CurrentPalletId", "DisableReason", "DisabledAt", "DisabledBy", "Enabled" },
                values: new object[,]
                {
                    { 1, null, null, null, null, true },
                    { 2, null, null, null, null, true },
                    { 3, null, null, null, null, true },
                    { 4, null, null, null, null, true },
                    { 5, null, null, null, null, true },
                    { 6, null, null, null, null, true },
                    { 7, null, null, null, null, true },
                    { 8, null, null, null, null, true },
                    { 9, null, null, null, null, true },
                    { 10, null, null, null, null, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_BasketId",
                schema: "unload",
                table: "BasketLines",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_OperatorId",
                schema: "unload",
                table: "BasketLines",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_PalletId",
                schema: "unload",
                table: "BasketLines",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_PartId",
                schema: "unload",
                table: "BasketLines",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_SessionId",
                schema: "unload",
                table: "BasketLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketLines_UnloadLineId",
                schema: "unload",
                table: "BasketLines",
                column: "UnloadLineId");

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
                name: "IX_CheckInEntries_PackingId",
                schema: "packing",
                table: "CheckInEntries",
                column: "PackingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckInEntries_SlotId",
                schema: "packing",
                table: "CheckInEntries",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckInSlots_CustomerOrderId",
                schema: "packing",
                table: "CheckInSlots",
                column: "CustomerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PackingDetails_PackingId",
                schema: "packing",
                table: "PackingDetails",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_PackingDetails_PickOrderId",
                schema: "packing",
                table: "PackingDetails",
                column: "PickOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PackingPartScans_PackingId",
                schema: "packing",
                table: "PackingPartScans",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_Packings_PalletId",
                schema: "packing",
                table: "Packings",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Packings_SortingPalletId",
                schema: "packing",
                table: "Packings",
                column: "SortingPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PackingId",
                schema: "master",
                table: "PartSerials",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PalletId",
                schema: "master",
                table: "PartSerials",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_PartId_SerialNo",
                schema: "master",
                table: "PartSerials",
                columns: new[] { "PartId", "SerialNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartSerials_ReceiptLineId",
                schema: "master",
                table: "PartSerials",
                column: "ReceiptLineId");

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
                name: "IX_PickOrders_CustomerOrderId",
                schema: "picking",
                table: "PickOrders",
                column: "CustomerOrderId");

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
                name: "IX_ReceiptLines_POId",
                schema: "receiving",
                table: "ReceiptLines",
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
                name: "IX_SortingBatchQueues_AssignedPalletId",
                schema: "sorting",
                table: "SortingBatchQueues",
                column: "AssignedPalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingBatchQueues_Status_QueuedAt",
                schema: "sorting",
                table: "SortingBatchQueues",
                columns: new[] { "Status", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_PackingId",
                schema: "sorting",
                table: "SortingPalletPacks",
                column: "PackingId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_PalletId",
                schema: "sorting",
                table: "SortingPalletPacks",
                column: "PalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SortingPalletPacks_Status_ScheduledAt",
                schema: "sorting",
                table: "SortingPalletPacks",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SortingStations_CurrentPalletId",
                schema: "sorting",
                table: "SortingStations",
                column: "CurrentPalletId");

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
                name: "BasketLines",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "CancelLog",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "CheckInEntries",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PackingDetails",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PackingPartScans",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PartSerials",
                schema: "master");

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
                name: "SortingBatchQueues",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortingPalletPacks",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "SortingStations",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "StationAuditLogs",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "WrappingSessions",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "Baskets",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "UnloadLines",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "CheckInSlots",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PickOrderDetails",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "ReceiptLines",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "Packings",
                schema: "packing");

            migrationBuilder.DropTable(
                name: "PutawaySessions",
                schema: "putaway");

            migrationBuilder.DropTable(
                name: "UnloadSessions",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "PickOrders",
                schema: "picking");

            migrationBuilder.DropTable(
                name: "Parts",
                schema: "master");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "receiving");

            migrationBuilder.DropTable(
                name: "SortingPallets",
                schema: "sorting");

            migrationBuilder.DropTable(
                name: "Pallets",
                schema: "unload");

            migrationBuilder.DropTable(
                name: "CustomerOrders",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "master");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "master");
        }
    }
}
