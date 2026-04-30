using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Putaway;

public class PutawaySessionConfiguration : IEntityTypeConfiguration<PutawaySession>
{
    public void Configure(EntityTypeBuilder<PutawaySession> builder)
    {
        builder
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
