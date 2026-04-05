using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PutawaySessions", Schema = "putaway")]
public class PutawaySession
{
    [Key]
    public int PutawayId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Status { get; set; } = "AGV_DISPATCHED";
    public bool WrappingRequired { get; set; } = false;
    public string OperatorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
}
