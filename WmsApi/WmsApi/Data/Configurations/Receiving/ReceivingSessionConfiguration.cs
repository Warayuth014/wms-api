using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Receiving;

public class ReceivingSessionConfiguration : IEntityTypeConfiguration<ReceivingSession>
{
    public void Configure(EntityTypeBuilder<ReceivingSession> builder)
    {
        builder
            .HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.POId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
