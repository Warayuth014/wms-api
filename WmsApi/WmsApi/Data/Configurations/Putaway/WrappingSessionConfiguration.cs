using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Putaway;

public class WrappingSessionConfiguration : IEntityTypeConfiguration<WrappingSession>
{
    public void Configure(EntityTypeBuilder<WrappingSession> builder)
    {
        builder
            .HasOne(x => x.PutawaySession)
            .WithMany()
            .HasForeignKey(x => x.PutawayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
