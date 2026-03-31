using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using WmsApi.Models;

namespace WmsApi.Data;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    // ── master ────────────────────────────────
    public DbSet<User> Users { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Part> Parts { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Tote> Totes { get; set; }
    public DbSet<ToteInventory> ToteInventory { get; set; }

    // ── flow1 ─────────────────────────────────
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<POItem> POItems { get; set; }
    public DbSet<ReceivingSession> ReceivingSessions { get; set; }
    public DbSet<ReceiptLine> ReceiptLines { get; set; }
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
    public DbSet<ReturnOrder> ReturnOrders { get; set; }
    public DbSet<ReturnLine> ReturnLines { get; set; }

    // ── flow2 ─────────────────────────────────
    public DbSet<Pallet> Pallets { get; set; }
    public DbSet<Basket> Baskets { get; set; }
    public DbSet<UnloadSession> UnloadSessions { get; set; }
    public DbSet<UnloadLine> UnloadLines { get; set; }
    public DbSet<BasketLine> BasketLines { get; set; }
    public DbSet<ReplenishOrder> ReplenishOrders { get; set; }
    public DbSet<ReplenishOrderLine> ReplenishOrderLines { get; set; }
    public DbSet<ReplenishSession> ReplenishSessions { get; set; }
    public DbSet<ReplenishSessionLine> ReplenishSessionLines { get; set; }

    // ── putaway ───────────────────────────────
    public DbSet<PutawaySession> PutawaySessions { get; set; }
    public DbSet<WrappingSession> WrappingSessions { get; set; }
    public DbSet<ShipXQueue> ShipXQueues { get; set; }

    // ── picking (v1) ─────────────────────────
    public DbSet<PickingSession> PickingSessions { get; set; }
    public DbSet<PickingLine> PickingLines { get; set; }

    // ── picking (v2 — Pick Order flow) ────────
    public DbSet<PickOrder> PickOrders { get; set; }
    public DbSet<PickOrderDetail> PickOrderDetails { get; set; }
    public DbSet<PickOrderSub> PickOrderSubs { get; set; }
    public DbSet<PickStation> PickStations { get; set; }

    // ── audit ─────────────────────────────────
    public DbSet<CancelLog> CancelLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    
    {
        // ── Customer ──────────────────────────────
        mb.Entity<SalesOrder>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.SalesOrders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<SalesOrder>()
            .HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ── SalesOrderItem ────────────────────────
        mb.Entity<SalesOrderItem>()
            .HasIndex(x => new { x.OrderId, x.PartId })
            .IsUnique();

        mb.Entity<SalesOrderItem>()
            .HasOne(x => x.SalesOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<SalesOrderItem>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReturnOrder ───────────────────────────
        mb.Entity<ReturnOrder>()
            .HasOne(x => x.SalesOrder)
            .WithMany(x => x.ReturnOrders)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReturnOrder>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReturnLine ────────────────────────────
        mb.Entity<ReturnLine>()
            .HasOne(x => x.ReturnOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.ReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ReturnLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReturnLine>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PurchaseOrder ─────────────────────
        mb.Entity<PurchaseOrder>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<PurchaseOrder>()
            .HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ── POItem ────────────────────────────
        mb.Entity<POItem>()
            .HasIndex(x => new { x.POId, x.PartId })
            .IsUnique();

        mb.Entity<POItem>()
            .HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.POId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<POItem>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReceivingSession ──────────────────
        mb.Entity<ReceivingSession>()
            .HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.POId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReceivingSession>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReceiptLine ───────────────────────
        mb.Entity<ReceiptLine>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ReceiptLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReceiptLine>()
            .HasOne(x => x.Pallet)
            .WithMany(x => x.ReceiptLines)
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        mb.Entity<ReceiptLine>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── UnloadSession ─────────────────────
        mb.Entity<UnloadSession>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<UnloadSession>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── UnloadLine ────────────────────────
        // ไม่ต้อง unique (SessionId, PartId) — รองรับ partial unload หลายครั้งต่อ Part
        mb.Entity<UnloadLine>()
            .HasOne(x => x.Session)
            .WithMany(x => x.UnloadLines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UnloadLine>()
            .HasOne(x => x.Pallet)
            .WithMany(x => x.UnloadLines)
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<UnloadLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<UnloadLine>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── BasketLine ────────────────────────
        mb.Entity<BasketLine>()
            .HasOne(x => x.Session)
            .WithMany(x => x.BasketLines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<BasketLine>()
            .HasOne(x => x.Basket)
            .WithMany(x => x.BasketLines)
            .HasForeignKey(x => x.BasketId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BasketLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BasketLine>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<BasketLine>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PutawaySession ────────────────────
        mb.Entity<PutawaySession>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<PutawaySession>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── WrappingSession ────────────────────
        mb.Entity<WrappingSession>()
            .HasOne(x => x.PutawaySession)
            .WithMany()
            .HasForeignKey(x => x.PutawayId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<WrappingSession>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ShipXQueue ──────────────────────────
        mb.Entity<ShipXQueue>()
            .HasOne(x => x.PutawaySession)
            .WithMany()
            .HasForeignKey(x => x.PutawayId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ShipXQueue>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickingSession ───────────────────
        mb.Entity<PickingSession>()
            .HasOne(x => x.PackPallet)
            .WithMany()
            .HasForeignKey(x => x.PackPalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<PickingSession>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickingLine ─────────────────────
        mb.Entity<PickingLine>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<PickingLine>()
            .HasOne(x => x.PickPallet)
            .WithMany()
            .HasForeignKey(x => x.PickPalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<PickingLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<PickingLine>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickOrder ────────────────────────
        mb.Entity<PickOrder>()
            .HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickOrderDetail ─────────────────
        mb.Entity<PickOrderDetail>()
            .HasOne(x => x.PickOrder)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.PickOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<PickOrderDetail>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickOrderSub ────────────────────
        mb.Entity<PickOrderSub>()
            .HasOne(x => x.PickOrderDetail)
            .WithMany(x => x.Subs)
            .HasForeignKey(x => x.PickOrderDetailId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<PickOrderSub>()
            .HasOne(x => x.ReceiptLine)
            .WithMany(x => x.PickOrderSubs)
            .HasForeignKey(x => x.ReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── PickStation ───────────────────────
        mb.Entity<PickStation>()
            .HasOne(x => x.CurrentPallet)
            .WithMany()
            .HasForeignKey(x => x.CurrentPalletId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // ── ToteInventory ─────────────────────
        mb.Entity<ToteInventory>()
            .HasIndex(x => new { x.ToteId, x.PartId })
            .IsUnique();

        mb.Entity<ToteInventory>()
            .HasOne(x => x.Tote)
            .WithMany(x => x.Inventory)
            .HasForeignKey(x => x.ToteId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ToteInventory>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReplenishOrderLine ─────────────────
        mb.Entity<ReplenishOrderLine>()
            .HasOne(x => x.ReplenishOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ReplenishOrderLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReplenishSession ───────────────────
        mb.Entity<ReplenishSession>()
            .HasOne(x => x.ReplenishOrder)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);  // Restrict ป้องกัน multi-path cascade

        mb.Entity<ReplenishSession>()
            .HasOne(x => x.Tote)
            .WithMany(x => x.ReplenishSessions)
            .HasForeignKey(x => x.ToteId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReplenishSession>()
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReplenishSession>()
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ReplenishSessionLine ───────────────
        mb.Entity<ReplenishSessionLine>()
            .HasOne(x => x.Session)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ReplenishSessionLine>()
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ReplenishSessionLine>()
            .HasOne(x => x.OrderLine)
            .WithMany(x => x.SessionLines)
            .HasForeignKey(x => x.OrderLineId)
            .OnDelete(DeleteBehavior.Restrict);  // Restrict ป้องกัน multi-path cascade

        // ── CancelLog ─────────────────────────
        mb.Entity<CancelLog>()
            .HasOne(x => x.Requester)
            .WithMany()
            .HasForeignKey(x => x.RequestBy)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<CancelLog>()
            .HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        base.OnModelCreating(mb);
    }
}