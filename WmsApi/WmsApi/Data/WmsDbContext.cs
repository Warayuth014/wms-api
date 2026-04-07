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

    // sorting
    public DbSet<SortStation> SortStations { get; set; }
    public DbSet<SortSession> SortSessions { get; set; }
    public DbSet<SortSessionItem> SortSessionItems { get; set; }

    // audit
    public DbSet<CancelLog> CancelLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);

        // Sorting: ป้องกัน multiple cascade paths ใน SQL Server
        mb.Entity<SortSessionItem>()
            .HasOne(i => i.SourcePallet)
            .WithMany()
            .HasForeignKey(i => i.SourcePalletId)
            .OnDelete(DeleteBehavior.NoAction);

        mb.Entity<SortSession>()
            .HasOne(s => s.SortPallet)
            .WithMany()
            .HasForeignKey(s => s.SortPalletId)
            .OnDelete(DeleteBehavior.NoAction);

        base.OnModelCreating(mb);
    }
}
