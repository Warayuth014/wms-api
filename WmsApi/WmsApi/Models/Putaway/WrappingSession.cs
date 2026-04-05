using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("WrappingSessions", Schema = "putaway")]
public class WrappingSession
{
    [Key]
    public int WrappingId { get; set; }
    public int PutawayId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(PutawayId))]
    public PutawaySession? PutawaySession { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }
}
