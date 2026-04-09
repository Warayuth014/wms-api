using Microsoft.EntityFrameworkCore;
using WmsApi.Models;

namespace WmsApi.Data;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    // master
    public DbSet<User> Users { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Part> Parts { get; set; }

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

    // basket
    public DbSet<Basket> Baskets { get; set; }
    public DbSet<BasketLine> BasketLines { get; set; }

    // audit
    public DbSet<CancelLog> CancelLogs { get; set; }

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
