using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("ReceivingSessions", Schema = "receiving")]
public class ReceivingSession
{
    [Key]
    public int SessionId { get; set; }
    public string POId { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    [ForeignKey(nameof(POId))]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    public ICollection<ReceiptLine> Lines { get; set; } = [];
}
