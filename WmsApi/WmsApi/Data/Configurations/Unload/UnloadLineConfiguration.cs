using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Unload;

public class UnloadLineConfiguration : IEntityTypeConfiguration<UnloadLine>
{
    public void Configure(EntityTypeBuilder<UnloadLine> builder)
    {
        builder
            .HasOne(x => x.Session)
            .WithMany(x => x.UnloadLines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Pallet)
            .WithMany(x => x.UnloadLines)
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
