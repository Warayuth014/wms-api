using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Receiving;

public class POItemLotConfiguration : IEntityTypeConfiguration<POItemLot>
{
    public void Configure(EntityTypeBuilder<POItemLot> builder)
    {
        builder
            .HasIndex(x => new { x.POItemId, x.LotNumber })
            .IsUnique();

        builder
            .HasOne(x => x.POItem)
            .WithMany(x => x.Lots)
            .HasForeignKey(x => x.POItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
