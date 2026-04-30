using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("POItems", Schema = "receiving")]
public class POItem
{
    [Key]
    public int Id { get; set; }
    public string POId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public int QtyReceived { get; set; } = 0;
    public int QtyRemaining { get; set; } = 0;
    public string Status { get; set; } = "PENDING";
    public string Condition { get; set; } = "FG";
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }

    [ForeignKey(nameof(POId))]
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }
}
