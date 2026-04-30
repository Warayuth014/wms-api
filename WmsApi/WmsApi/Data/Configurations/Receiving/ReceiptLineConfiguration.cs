using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WmsApi.Models;

namespace WmsApi.Data.Configurations.Receiving;

public class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
{
    public void Configure(EntityTypeBuilder<ReceiptLine> builder)
    {
        builder
            .HasOne(x => x.Session)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Pallet)
            .WithMany(x => x.ReceiptLines)
            .HasForeignKey(x => x.PalletId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder
            .HasOne(x => x.Operator)
            .WithMany()
            .HasForeignKey(x => x.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
