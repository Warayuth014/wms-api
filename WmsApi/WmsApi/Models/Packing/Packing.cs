using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("Packings", Schema = "packing")]
public class Packing
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string PackingId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string Owner { get; set; } = string.Empty;

    public string Status { get; set; } = "OPEN"; // OPEN | DONE

    [Column(TypeName = "nvarchar(50)")]
    public string? PickOrderId { get; set; }

    public int SeqNo { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string? TrackingId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    public ICollection<PackingDetail> Details { get; set; } = [];
}
