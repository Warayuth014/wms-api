using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Putaway;

public class PreworkCutLogConfiguration : IEntityTypeConfiguration<PreworkCutLog>
{
    public void Configure(EntityTypeBuilder<PreworkCutLog> builder)
    {
        builder
            .HasOne(x => x.Pallet)
            .WithMany()
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
