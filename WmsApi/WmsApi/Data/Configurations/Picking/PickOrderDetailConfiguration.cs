using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Picking;

public class PickOrderDetailConfiguration : IEntityTypeConfiguration<PickOrderDetail>
{
    public void Configure(EntityTypeBuilder<PickOrderDetail> builder)
    {
        builder
            .HasOne(x => x.PickOrder)
            .WithMany(x => x.Details)
            .HasForeignKey(x => x.PickOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
