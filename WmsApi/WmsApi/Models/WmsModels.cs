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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// =============================================
// Schema: flow1
// =============================================

[Table("PurchaseOrders", Schema = "flow1")]
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

[Table("POItems", Schema = "flow1")]
public class POItem
{
    [Key]
    public int Id { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public int QtyReceived { get; set; } = 0;
    public string Status { get; set; } = "PENDING";

    // Navigation
    [ForeignKey(nameof(POId))]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }
}

[Table("ReceivingSessions", Schema = "flow1")]
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

[Table("ReceiptLines", Schema = "flow1")]
public class ReceiptLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string? PalletId { get; set; }   // NULL ก่อน ผูกทีหลัง
    public int QtyReceived { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string Condition { get; set; } = "NORMAL";
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

    // PalletId Navigation ใส่หลังสร้าง Pallet class
    // เพิ่มใต้ Operator Navigation
    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }
}

// =============================================
// Schema: flow2
// =============================================

[Table("Pallets", Schema = "flow2")]
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

[Table("Baskets", Schema = "flow2")]
public class Basket
{
    [Key]
    public string BasketId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Destination { get; set; }
    public string Status { get; set; } = "AVAILABLE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<BasketLine> BasketLines { get; set; } = [];
}

[Table("UnloadSessions", Schema = "flow2")]
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
    public ICollection<BasketLine> BasketLines { get; set; } = [];
}

[Table("UnloadLines", Schema = "flow2")]
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

[Table("BasketLines", Schema = "flow2")]
public class BasketLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string BasketId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string PalletId { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public int QtyLoaded { get; set; }
    public string Status { get; set; } = "LOADED";
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    public string OperatorId { get; set; } = string.Empty;

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public UnloadSession? Session { get; set; }

    [ForeignKey(nameof(BasketId))]
    public Basket? Basket { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
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