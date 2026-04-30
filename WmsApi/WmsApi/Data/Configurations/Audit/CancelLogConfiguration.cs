using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Audit;

public class CancelLogConfiguration : IEntityTypeConfiguration<CancelLog>
{
    public void Configure(EntityTypeBuilder<CancelLog> builder)
    {
        builder
            .HasOne(x => x.Requester)
            .WithMany()
            .HasForeignKey(x => x.RequestBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
