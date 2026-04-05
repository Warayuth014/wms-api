using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Picking;

public class PickOrderSubConfiguration : IEntityTypeConfiguration<PickOrderSub>
{
    public void Configure(EntityTypeBuilder<PickOrderSub> builder)
    {
        builder
            .HasOne(x => x.PickOrderDetail)
            .WithMany(x => x.Subs)
            .HasForeignKey(x => x.PickOrderDetailId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ReceiptLine)
            .WithMany(x => x.PickOrderSubs)
            .HasForeignKey(x => x.ReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
