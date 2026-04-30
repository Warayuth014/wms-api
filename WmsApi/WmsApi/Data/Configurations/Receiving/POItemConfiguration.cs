using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Receiving;

public class POItemConfiguration : IEntityTypeConfiguration<POItem>
{
    public void Configure(EntityTypeBuilder<POItem> builder)
    {
        builder
            .HasIndex(x => new { x.POId, x.PartId })
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
