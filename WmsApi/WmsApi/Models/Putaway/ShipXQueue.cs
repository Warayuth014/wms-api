using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("ShipXQueue", Schema = "putaway")]
public class ShipXQueue
{
    [Key]
    public int QueueId { get; set; }
    public int PutawayId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "QUEUED";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    [ForeignKey(nameof(PutawayId))]
    public PutawaySession? PutawaySession { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }
}
