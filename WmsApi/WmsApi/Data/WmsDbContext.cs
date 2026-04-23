using Microsoft.EntityFrameworkCore;
using WmsApi.Models;

namespace WmsApi.Data;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    // master
    public DbSet<User> Users { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Part> Parts { get; set; }
    public DbSet<PartSerial> PartSerials { get; set; }

    // receiving
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<POItem> POItems { get; set; }
    public DbSet<ReceivingSession> ReceivingSessions { get; set; }
    public DbSet<ReceiptLine> ReceiptLines { get; set; }

    // unload
    public DbSet<Pallet> Pallets { get; set; }
    public DbSet<UnloadSession> UnloadSessions { get; set; }
    public DbSet<UnloadLine> UnloadLines { get; set; }

    // putaway
    public DbSet<PutawaySession> PutawaySessions { get; set; }
    public DbSet<WrappingSession> WrappingSessions { get; set; }
    public DbSet<ShipXQueue> ShipXQueues { get; set; }
    public DbSet<PreworkCutLog> PreworkCutLogs { get; set; }

    // picking
    public DbSet<PickOrder> PickOrders { get; set; }
    public DbSet<PickOrderDetail> PickOrderDetails { get; set; }
    public DbSet<PickOrderSub> PickOrderSubs { get; set; }
    public DbSet<PickStation> PickStations { get; set; }

    // packing
    public DbSet<Packing> Packings { get; set; }
    public DbSet<PackingDetail> PackingDetails { get; set; }
    public DbSet<PackingPartScan> PackingPartScans { get; set; }
    public DbSet<CheckInSlot> CheckInSlots { get; set; }
    public DbSet<CheckInEntry> CheckInEntries { get; set; }

    // basket
    public DbSet<Basket> Baskets { get; set; }
    public DbSet<BasketLine> BasketLines { get; set; }

    // audit
    public DbSet<CancelLog> CancelLogs { get; set; }

    // customer
    public DbSet<CustomerOrder> CustomerOrders { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);

        // Packing: ป้องกัน multiple cascade paths ใน SQL Server
        mb.Entity<PackingDetail>()
            .HasOne(d => d.PickOrder)
            .WithMany()
            .HasForeignKey(d => d.PickOrderId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<Packing>()
            .HasOne(p => p.Pallet)
            .WithMany()
            .HasForeignKey(p => p.PalletId)
            .OnDelete(DeleteBehavior.NoAction);

        // CheckIn
        mb.Entity<CheckInEntry>()
            .HasOne(e => e.Slot)
            .WithMany(s => s.Entries)
            .HasForeignKey(e => e.SlotId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<CheckInEntry>()
            .HasOne(e => e.Packing)
            .WithMany()
            .HasForeignKey(e => e.PackingId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<CheckInEntry>()
            .HasIndex(e => e.PackingId)
            .IsUnique();

        // PartSerial: unique (PartId, SerialNo)
        mb.Entity<PartSerial>()
            .HasIndex(s => new { s.PartId, s.SerialNo })
            .IsUnique();

        mb.Entity<PartSerial>()
            .HasOne(s => s.Packing)
            .WithMany()
            .HasForeignKey(s => s.PackingId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<PartSerial>()
            .HasOne(s => s.Pallet)
            .WithMany()
            .HasForeignKey(s => s.PalletId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<PartSerial>()
            .HasOne(s => s.ReceiptLine)
            .WithMany()
            .HasForeignKey(s => s.ReceiptLineId)
            .OnDelete(DeleteBehavior.NoAction);

        // CustomerOrder relationships (nullable FKs, prevent cascade issues)
        mb.Entity<PickOrder>()
            .HasOne(p => p.CustomerOrder)
            .WithMany(c => c.PickOrders)
            .HasForeignKey(p => p.CustomerOrderId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<CheckInSlot>()
            .HasOne(s => s.CustomerOrder)
            .WithMany()
            .HasForeignKey(s => s.CustomerOrderId)
            .OnDelete(DeleteBehavior.NoAction);

        // Basket: ป้องกัน multiple cascade paths
        mb.Entity<BasketLine>()
            .HasOne(l => l.Basket)
            .WithMany(b => b.Lines)
            .HasForeignKey(l => l.BasketId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<BasketLine>()
            .HasOne(l => l.UnloadLine)
            .WithMany()
            .HasForeignKey(l => l.UnloadLineId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<BasketLine>()
            .HasOne(l => l.Pallet)
            .WithMany()
            .HasForeignKey(l => l.PalletId)
            .OnDelete(DeleteBehavior.NoAction);

        base.OnModelCreating(mb);
    }
}
