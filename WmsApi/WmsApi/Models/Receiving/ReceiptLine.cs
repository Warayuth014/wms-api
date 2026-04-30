using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("ReceiptLines", Schema = "receiving")]
public class ReceiptLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string? PalletId { get; set; }
    public int QtyReceived { get; set; }
    public string Condition { get; set; } = "FG";
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SessionId))]
    public ReceivingSession? Session { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    public ICollection<PickOrderSub> PickOrderSubs { get; set; } = [];
}
