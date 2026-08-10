using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Receiving;

public class POItemConfiguration : IEntityTypeConfiguration<POItem>
{
    public void Configure(EntityTypeBuilder<POItem> builder)
    {
        // Part เดียวกันรับได้ทั้ง FG และ PW ใน PO เดียวกัน (คนละ line) — unique รวม Condition ด้วย
        builder
            .HasIndex(x => new { x.POId, x.PartId, x.Condition })
            .IsUnique();

        builder
            .HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.POId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
