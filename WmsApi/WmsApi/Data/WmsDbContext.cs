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

    // ── flow1 ─────────────────────────────────
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<POItem> POItems { get; set; }
    public DbSet<ReceivingSession> ReceivingSessions { get; set; }
    public DbSet<ReceiptLine> ReceiptLines { get; set; }

    // ── flow2 ─────────────────────────────────
    public DbSet<Pallet> Pallets { get; set; }
    public DbSet<Basket> Baskets { get; set; }
    public DbSet<UnloadSession> UnloadSessions { get; set; }
    public DbSet<UnloadLine> UnloadLines { get; set; }
    public DbSet<BasketLine> BasketLines { get; set; }

    // ── audit ─────────────────────────────────
    public DbSet<CancelLog> CancelLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
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
        mb.Entity<UnloadLine>()
            .HasIndex(x => new { x.SessionId, x.PartId })
            .IsUnique();

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