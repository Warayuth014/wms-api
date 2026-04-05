using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Picking;

public class PickOrderConfiguration : IEntityTypeConfiguration<PickOrder>
{
    public void Configure(EntityTypeBuilder<PickOrder> builder)
    {
        builder
            .HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
