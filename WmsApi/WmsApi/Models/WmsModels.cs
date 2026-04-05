using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Metrics;

namespace WmsApi.Models;

// =============================================
// Schema: master
// =============================================

[Table("Users", Schema = "master")]
public class User
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "OPERATOR";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("Suppliers", Schema = "master")]
public class Supplier
{
    [Key]
    public string SupplierId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}

[Table("Parts", Schema = "master")]
public class Part
{
    [Key]
    public string PartId { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ItemDesc { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }                 // /uploads/parts/xxx.jpg
    public int? MinStock { get; set; }                    // เติมเมื่อ OnHand < MinStock
    public int? MaxStock { get; set; }                    // เติมให้ถึง MaxStock
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// =============================================
// Schema: receiving
// =============================================

[Table("PurchaseOrders", Schema = "receiving")]
public class PurchaseOrder
{
    [Key]
    public string POId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SupplierId))]
    public Supplier? Supplier { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    public ICollection<POItem> Items { get; set; } = [];
}

[Table("POItems", Schema = "receiving")]
public class POItem
{
    [Key]
    public int Id { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public int QtyReceived { get; set; } = 0;
    public int QtyRemaining { get; set; } = 0;
    public string Status { get; set; } = "PENDING";
    public string Condition { get; set; } = "FG";       // FG | PW
    public string? LotNumber { get; set; }               // Lot/Batch
    public DateOnly? ExpiredDate { get; set; }            // วันหมดอายุ

    // Navigation
    [ForeignKey(nameof(POId))]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }
}

[Table("ReceivingSessions", Schema = "receiving")]
public class ReceivingSession
{
    [Key]
    public int SessionId { get; set; }
    public string POId { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(POId))]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    public ICollection<ReceiptLine> Lines { get; set; } = [];
}

[Table("ReceiptLines", Schema = "receiving")]
public class ReceiptLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string? PalletId { get; set; }   // NULL ก่อน ผูกทีหลัง
    public int QtyReceived { get; set; }
    public string Condition { get; set; } = "FG";       // FG | PW — copy จาก POItem
    public string? LotNumber { get; set; }               // copy จาก POItem
    public DateOnly? ExpiredDate { get; set; }            // copy จาก POItem
    public string Status { get; set; } = "PENDING";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public ReceivingSession? Session { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    public ICollection<PickOrderSub> PickOrderSubs { get; set; } = [];
}

// =============================================
// Schema: unload
// =============================================

[Table("Pallets", Schema = "unload")]
public class Pallet
{
    [Key]
    public string PalletId { get; set; } = string.Empty;
    public string? Type { get; set; }   // FG | PW | DAMAGED
    public string Status { get; set; } = "AVAILABLE";
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ReceiptLine> ReceiptLines { get; set; } = [];
    public ICollection<UnloadLine> UnloadLines { get; set; } = [];
}

[Table("UnloadSessions", Schema = "unload")]
public class UnloadSession
{
    [Key]
    public int SessionId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string Status { get; set; } = "STEP1";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime? Step1DoneAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    public ICollection<UnloadLine> UnloadLines { get; set; } = [];
}

[Table("UnloadLines", Schema = "unload")]
public class UnloadLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public int QtyUnloaded { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime? ConfirmedAt { get; set; }
    public string OperatorId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public UnloadSession? Session { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
}

// =============================================
// Schema: putaway
// =============================================

[Table("PutawaySessions", Schema = "putaway")]
public class PutawaySession
{
    [Key]
    public int PutawayId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty; // ASRS | PREWORK | REPLENISH
    public string Status { get; set; } = "AGV_DISPATCHED";  // WRAPPING | AGV_DISPATCHED | COMPLETED
    public bool WrappingRequired { get; set; } = false;
    public string OperatorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
}

[Table("WrappingSessions", Schema = "putaway")]
public class WrappingSession
{
    [Key]
    public int WrappingId { get; set; }
    public int PutawayId { get; set; }
    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING | COMPLETED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(PutawayId))]
    public PutawaySession? PutawaySession { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }
}

[Table("ShipXQueue", Schema = "putaway")]
public class ShipXQueue
{
    [Key]
    public int QueueId { get; set; }
    public int PutawayId { get; set; }
    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; // JSON — ข้อมูลที่จะส่งให้ SHIP-X
    public string Status { get; set; } = "QUEUED";      // QUEUED | SENT | FAILED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    // Navigation
    [ForeignKey(nameof(PutawayId))]
    public PutawaySession? PutawaySession { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }
}

// ── PreworkCutLog — บันทึกสินค้าที่ตัดยอดออกจาก Pallet ที่จุด Prework ──
[Table("PreworkCutLogs", Schema = "putaway")]
public class PreworkCutLog
{
    [Key]
    public int Id { get; set; }
    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(20)")]
    public string StationId { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public string? Brand { get; set; }
    public string? ItemDesc { get; set; }
    public string? ImageUrl { get; set; }
    public int Qty { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string Condition { get; set; } = "PW";
    [Column(TypeName = "nvarchar(20)")]
    public string OperatorId { get; set; } = "SYSTEM";
    public DateTime CutAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }
}

// =============================================
// Schema: audit
// =============================================

[Table("CancelLog", Schema = "audit")]
public class CancelLog
{
    [Key]
    public int CancelId { get; set; }
    public string RefType { get; set; } = string.Empty;
    // ReceiptLine | UnloadLine | BasketLine
    public int RefId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string Status { get; set; } = "PENDING";
    // PENDING | APPROVED | REJECTED
    public DateTime? CancelledAt { get; set; }

    // Navigation
    [ForeignKey(nameof(RequestBy))]
    public User? Requester { get; set; }

    [ForeignKey(nameof(ApprovedBy))]
    public User? Approver { get; set; }
}


// =============================================
// Schema: picking  (v2 — Pick Order flow)
// =============================================

[Table("PickOrders", Schema = "picking")]
public class PickOrder
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN"; // OPEN | COMPLETED
    [Column(TypeName = "nvarchar(50)")]
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    // Navigation
    public ICollection<PickOrderDetail> Details { get; set; } = [];
}

[Table("PickOrderDetails", Schema = "picking")]
public class PickOrderDetail
{
    [Key]
    public int Id { get; set; }
    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;
    public int RequiredQty { get; set; }
    public int ReservedQty { get; set; } = 0;
    public string Status { get; set; } = "PENDING"; // PENDING | PARTIAL | COMPLETED

    // Navigation
    [ForeignKey(nameof(PickOrderId))]
    public PickOrder? PickOrder { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    public ICollection<PickOrderSub> Subs { get; set; } = [];
}

[Table("PickOrderSubs", Schema = "picking")]
public class PickOrderSub
{
    [Key]
    public int Id { get; set; }
    public int PickOrderDetailId { get; set; }
    public int ReceiptLineId { get; set; }
    public int AllocatedQty { get; set; }
    public int PickedQty { get; set; } = 0;
    public string Status { get; set; } = "PENDING"; // PENDING | PICKED

    // Navigation
    [ForeignKey(nameof(PickOrderDetailId))]
    public PickOrderDetail? PickOrderDetail { get; set; }

    [ForeignKey(nameof(ReceiptLineId))]
    public ReceiptLine? ReceiptLine { get; set; }
}

[Table("PickStations", Schema = "picking")]
public class PickStation
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string StationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(50)")]
    public string? CurrentPalletId { get; set; } // null = station is free

    [ForeignKey(nameof(CurrentPalletId))]
    public Pallet? CurrentPallet { get; set; }
}
