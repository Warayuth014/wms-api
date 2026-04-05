using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Picking;

public class PickStationConfiguration : IEntityTypeConfiguration<PickStation>
{
    public void Configure(EntityTypeBuilder<PickStation> builder)
    {
        builder
            .HasOne(x => x.CurrentPallet)
            .WithMany()
            .HasForeignKey(x => x.CurrentPalletId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
